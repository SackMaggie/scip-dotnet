using System.Diagnostics;
using Google.Protobuf;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scip;

namespace ScipDotnet;

public static class IndexCommandHandler
{
    public static async Task<int> Process(
        IHost host,
        List<FileInfo> projects,
        string output,
        FileInfo workingDirectory,
        List<string> include,
        List<string> exclude,
        bool allowGlobalSymbolDefinitions,
        int dotnetRestoreTimeout,
        bool skipDotnetRestore,
        FileInfo? nugetConfigPath,
        FileInfo? projectOutcomes
        )
    {
        var logger = host.Services.GetRequiredService<ILogger<IndexCommandOptions>>();
        var matcher = new Matcher();
        matcher.AddIncludePatterns(include.Count == 0 ? new[] { "**" } : include);
        matcher.AddExcludePatterns(exclude);

        var projectFiles = projects.Count > 0
            ? projects
            : FindSolutionOrProjectFile(workingDirectory, logger);
        if (!projectFiles.Any())
        {
            return 1;
        }

        var options = new IndexCommandOptions(
            workingDirectory,
            OutputFile(workingDirectory, output),
            projectFiles,
            logger,
            matcher,
            allowGlobalSymbolDefinitions,
            dotnetRestoreTimeout,
            skipDotnetRestore,
            nugetConfigPath,
            projectOutcomes == null ? null : OutputFile(workingDirectory, projectOutcomes.ToString())
        );
        await ScipIndex(host, options);


        // Log msbuild workspace diagnostic information after the index command finishes
        // We log the MSBuild failures as error since they are often blocking issues
        // preventing indexing. However, we log msbuild warnings as debug since they
        // do not block indexing usually and are much noisier
        var workspaceLogger = host.Services.GetRequiredService<ILogger<MSBuildWorkspace>>();
        var workspaceService = host.Services.GetRequiredService<MSBuildWorkspace>();
        if (workspaceService.Diagnostics.Any())
        {
            var diagnosticGroups = workspaceService.Diagnostics
                .GroupBy(d => new { d.Kind, d.Message })
                .Select(g => new { g.Key.Kind, g.Key.Message, Count = g.Count() });
            foreach (var diagnostic in diagnosticGroups)
            {
                var message = $"{diagnostic.Kind}: {diagnostic.Message} (occurred {diagnostic.Count} times)";
                if (diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
                {
                    workspaceLogger.LogError(message);
                }
                else
                {
                    workspaceLogger.LogDebug(message);
                }
            }
        }

        return 0;
    }

    private static FileInfo OutputFile(FileInfo workingDirectory, string output) =>
        Path.IsPathRooted(output) ? new FileInfo(output) : new FileInfo(Path.Join(workingDirectory.FullName, output));

    private static async Task ScipIndex(IHost host, IndexCommandOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var indexer = host.Services.GetRequiredService<ScipProjectIndexer>();
        var metadata = new Metadata
        {
            ProjectRoot = new Uri(new Uri("file://"), options.WorkingDirectory.FullName).ToString(),
            ToolInfo = new ToolInfo
            {
                Name = "scip-dotnet",
                Version = "0.1.0-SNAPSHOT"
            },
            TextDocumentEncoding = TextEncoding.Utf8,
        };
        var outcomes = new ProjectOutcomes();
        var written = await WriteIndexFileAsync(options.Output, metadata,
            indexer.IndexDocuments(host, options, outcomes));

        if (written <= 0)
        {
            options.Logger.LogWarning("Indexing finished without error but no documents were indexed.");
        }

        options.Logger.LogInformation("projects: {Indexed} of {Total} indexed",
            outcomes.Count(ProjectOutcomes.StatusIndexed), outcomes.Total);
        if (options.ProjectOutcomes != null)
        {
            await outcomes.WriteAsync(options.ProjectOutcomes);
        }
        options.Logger.LogInformation("done: {OptionsOutput} {TimeElapsed}", options.Output,
            stopwatch.Elapsed.ToFriendlyString());
    }

    // Written beside the output and renamed onto it only once complete, so a run that dies midway
    // leaves no half an index where a whole one is expected.
    private const string PartialSuffix = ".partial";

    public static async Task<int> WriteIndexFileAsync(FileInfo output, Metadata metadata,
        IAsyncEnumerable<Document> documents)
    {
        var partial = output.FullName + PartialSuffix;
        try
        {
            int written;
            await using (var stream = File.Create(partial))
            {
                written = await WriteIndexAsync(stream, metadata, documents);
            }
            File.Move(partial, output.FullName, overwrite: true);
            return written;
        }
        finally
        {
            File.Delete(partial);
        }
    }

    /// <summary>Writes the index one document at a time, so no document outlives its own write. The
    /// bytes are exactly <c>Index.ToByteArray()</c>'s: metadata as field 1, each document as field 2.</summary>
    public static async Task<int> WriteIndexAsync(Stream output, Metadata metadata,
        IAsyncEnumerable<Document> documents)
    {
        using var coded = new CodedOutputStream(output, leaveOpen: true);
        coded.WriteTag(Scip.Index.MetadataFieldNumber, WireFormat.WireType.LengthDelimited);
        coded.WriteMessage(metadata);
        var written = 0;
        await foreach (var document in documents)
        {
            coded.WriteTag(Scip.Index.DocumentsFieldNumber, WireFormat.WireType.LengthDelimited);
            coded.WriteMessage(document);
            coded.Flush();
            written++;
        }
        coded.Flush();
        return written;
    }

    private static string FixThisProblem(string examplePath) =>
        "To fix this problem, pass the path of a solution (.sln/.slnx) or project (.csproj/.vbproj) file to the `scip-dotnet index` command. " +
        $"For example, run: scip-dotnet index {examplePath}";

    private static List<FileInfo> FindSolutionOrProjectFile(FileInfo workingDirectory, ILogger logger)
    {
        var paths = Directory.GetFiles(workingDirectory.FullName).Where(file =>
            string.Equals(Path.GetExtension(file), ".sln", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(file), ".slnx", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(file), ".csproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(file), ".vbproj", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (paths.Count != 0)
        {
            return paths.Select(path => new FileInfo(path)).ToList();
        }

        logger.LogError(
            "No solution (.sln/.slnx) or .csproj/.vbproj file detected in the working directory '{WorkingDirectory}'. {FixThis}",
            workingDirectory.FullName, FixThisProblem("SOLUTION_FILE"));
        return new List<FileInfo>();
    }
}