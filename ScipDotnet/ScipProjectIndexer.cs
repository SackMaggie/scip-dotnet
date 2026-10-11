using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScipDotnet;

/// <summary>
/// Orchestrates Roslyn and MSBuild APIs to SCIP index a given project.
/// </summary>
public class ScipProjectIndexer
{
    public ScipProjectIndexer(ILogger<ScipProjectIndexer> logger) =>
        Logger = logger;

    private ILogger<ScipProjectIndexer> Logger { get; }

    private (string Status, int? ExitCode) Restore(IndexCommandOptions options, FileInfo project)
    {
        // Every root is restored by its own path: a bare `dotnet restore` reads the working directory,
        // which holds no project when the root sits below it, so the project went unrestored (MSB1003).
        var arguments = $"restore \"{project.FullName}\" /p:EnableWindowsTargeting=true";
        if (options.NugetConfigPath != null)
        {
            arguments += $" --configfile \"{options.NugetConfigPath.FullName}\"";
        }
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                WorkingDirectory = options.WorkingDirectory.FullName,
                FileName = "dotnet",
                Arguments = arguments
            }
        };
        options.Logger.LogInformation("$ dotnet {Arguments}", arguments);
        process.Start();
        if (!process.WaitForExit(options.DotnetRestoreTimeout))
        {
            Logger.LogWarning("Dotnet restore did not finish in {Time} milliseconds, the results of the indexing might be incorrect.", options.DotnetRestoreTimeout);
            return (ProjectOutcomes.RestoreTimedOut, null);
        }

        return (process.ExitCode == 0 ? ProjectOutcomes.RestoreSucceeded : ProjectOutcomes.RestoreFailed,
            process.ExitCode);
    }

    public async IAsyncEnumerable<Scip.Document> IndexDocuments(IHost host, IndexCommandOptions options,
                                                                ProjectOutcomes outcomes)
    {
        // By project file, not ProjectId: every root opens a solution of its own, whose ids are
        // new, so a project two roots share would otherwise be written once per root.
        var indexedProjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in options.ProjectsFile)
        {
            await foreach (var document in IndexProject(host, options, project, indexedProjects, outcomes))
            {
                yield return document;
            }
        }
    }

    /// <summary>Every project file the workspace began to load, reported synchronously so none
    /// arrives after the solution is opened.</summary>
    private sealed class AttemptedProjects : IProgress<ProjectLoadProgress>
    {
        private readonly object _gate = new();
        private readonly List<string> _paths = new();

        public void Add(string path)
        {
            lock (_gate)
            {
                if (!_paths.Contains(path, StringComparer.OrdinalIgnoreCase)) _paths.Add(path);
            }
        }

        public void Report(ProjectLoadProgress value) => Add(value.FilePath);

        public List<string> Paths()
        {
            lock (_gate) return new List<string>(_paths);
        }
    }

    private async IAsyncEnumerable<Scip.Document> IndexProject(IHost host,
                                                               IndexCommandOptions options,
                                                               FileInfo rootProject,
                                                               HashSet<string> indexedProjects,
                                                               ProjectOutcomes outcomes)
    {
        var restore = options.SkipDotnetRestore
            ? (ProjectOutcomes.RestoreNotRun, (int?)null)
            : Restore(options, rootProject);
        outcomes.Root(options.WorkingDirectory, rootProject, restore.Item1, restore.Item2);

        var workspace = host.Services.GetRequiredService<MSBuildWorkspace>();
        var attempted = new AttemptedProjects();
        var isProjectFile = string.Equals(rootProject.Extension, ".csproj", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(rootProject.Extension, ".vbproj", StringComparison.OrdinalIgnoreCase);
        if (isProjectFile)
        {
            attempted.Add(rootProject.FullName);
        }
        var projects = (isProjectFile
            ? new[]
            {
                await workspace.OpenProjectAsync(rootProject.FullName, progress: attempted)
            }
            : (await workspace.OpenSolutionAsync(rootProject.FullName, progress: attempted)).Projects).ToList();


        options.Logger.LogDebug($"Found {projects.Count()} projects");
        var projectsPerProjFile = projects.GroupBy(x => x.FilePath).ToList();
        var framework = $"net{Environment.Version.Major}.0";
        foreach (var projectGroup in projectsPerProjFile)
        {

            // If the project was found by opening the solution, we need to find the project that matches the framework.
            // if we can' fall back to the first one. Without this, we will process the same document multiple times
            // once for each framework version being targeting and it leads to unpredictable results since the scip file
            // will contain the same document multiple times iwth different symbols.
            var project = projectGroup.FirstOrDefault(x => x.Name.Contains($"({framework})", StringComparison.OrdinalIgnoreCase)) ?? projectGroup.First();
            if (project.Language != "C#" && project.Language != "Visual Basic")
            {
                Logger.LogWarning(
                    "Skipping project {ProjectFilePath} because it has language {ProjectLanguage} and scip-dotnet currently only supports C# and Visual Basic.",
                    project.FilePath, project.Language);
                if (project.FilePath != null)
                {
                    outcomes.Project(options.WorkingDirectory, rootProject, project.FilePath,
                        ProjectOutcomes.StatusSkipped, 0,
                        new[] { $"language: {project.Language} is not indexed by scip-dotnet" });
                }
                continue;
            }

            if (!indexedProjects.Add(project.FilePath ?? project.Id.Id.ToString()))
            {
                continue;
            }

            var globals = new Dictionary<ISymbol, ScipSymbol>(SymbolEqualityComparer.Default);
            var documents = 0;

            options.Logger.LogDebug($"Found {project.Documents.Count()} documents in {projectGroup.Key}");
            foreach (var document in project.Documents)
            {
                if (options.Matcher.Match(options.WorkingDirectory.FullName, document.FilePath).HasMatches)
                {
                    documents++;
                    yield return await IndexDocument(document, options, globals, project.Language);
                }
                else
                {
                    options.Logger.LogDebug(
                        "Excluded file path '{FilePath}' because it did not match the provided --include and --exclude arguments",
                        document.FilePath);
                }
            }

            if (project.FilePath != null)
            {
                // A project the workspace still loaded after a failed restore or design-time build
                // is indexed from its source files without the references that build resolves, so
                // its symbols are incomplete and it never reads as indexed.
                var reasons = ProjectOutcomes.FailureReasons(options.WorkingDirectory, project.FilePath, workspace);
                if (reasons.Count > 0)
                {
                    Logger.LogError("Project {ProjectFilePath} was only partially indexed: {Reasons}",
                        project.FilePath, string.Join("; ", reasons));
                }
                outcomes.Project(options.WorkingDirectory, rootProject, project.FilePath,
                    reasons.Count == 0 ? ProjectOutcomes.StatusIndexed : ProjectOutcomes.StatusPartial,
                    documents, reasons);
            }
        }

        var loaded = new HashSet<string>(projectsPerProjFile.Select(g => g.Key).OfType<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var path in attempted.Paths().Where(p => !loaded.Contains(p)))
        {
            var reasons = ProjectOutcomes.FailureReasons(options.WorkingDirectory, path, workspace);
            if (reasons.Count == 0)
            {
                reasons.Add("load: the workspace did not load this project and recorded no failure for it");
            }
            Logger.LogError("Project {ProjectFilePath} was not indexed: {Reasons}", path, string.Join("; ", reasons));
            outcomes.Project(options.WorkingDirectory, rootProject, path, ProjectOutcomes.StatusNotIndexed, 0, reasons);
        }
    }

    private async Task<Scip.Document> IndexDocument(Document document,
                                                    IndexCommandOptions options,
                                                    Dictionary<ISymbol, ScipSymbol> globals,
                                                    string language)
    {
        Scip.Document doc = new()
        {
            Language = language,
            RelativePath = document.FilePath == null
                ? null
                : Path.GetRelativePath(options.WorkingDirectory.FullName, document.FilePath)
        };
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
        {
            Logger.LogWarning(
                "Skipping document {DocumentFilePath} because document.GetSemanticModelAsync() returned null",
                document.FilePath);
        }
        else
        {
            var symbolFormatter = new ScipDocumentIndexer(doc, options, globals);
            var root = await document.GetSyntaxRootAsync();
            if (language == "C#")
            {
                var walker = new ScipCSharpSyntaxWalker(symbolFormatter, semanticModel);
                walker.Visit(root);
            }
            else if (language == "Visual Basic")
            {
                var walker = new ScipVisualBasicSyntaxWalker(symbolFormatter, semanticModel);
                walker.Visit(root);
            }
        }

        return doc;
    }
}
