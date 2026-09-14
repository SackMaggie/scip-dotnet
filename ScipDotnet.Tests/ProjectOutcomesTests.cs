using System.Diagnostics;
using System.Text.Json;

namespace ScipDotnet.Tests;

/// <summary>
/// The --project-outcomes record, read the way a consumer reads it: from the file a real index run
/// wrote. Every fixture restores offline -- its NuGet.config clears every source and names an empty
/// local feed -- so a restore failure here never depends on the network.
/// </summary>
[TestFixture]
public class ProjectOutcomesTests
{
    [Test]
    public void AProjectWhoseRestoreFailedIsNamedWithItsReasonAndNeverReadsAsIndexed()
    {
        var outcomes = Index("restore-failure", "Mixed.slnx");

        Assert.That(outcomes.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
        var root = outcomes.GetProperty("roots").EnumerateArray().Single();
        Assert.That(root.GetProperty("path").GetString(), Is.EqualTo("Mixed.slnx"));
        Assert.That(root.GetProperty("restore").GetString(), Is.EqualTo("failed"));

        var projects = ByProject(outcomes);
        Assert.That(projects.Keys, Is.EquivalentTo(new[] { "Broken/Broken.csproj", "Good/Good.csproj" }));
        Assert.That(projects["Good/Good.csproj"].GetProperty("status").GetString(), Is.EqualTo("indexed"));
        Assert.That(projects["Good/Good.csproj"].GetProperty("reasons").GetArrayLength(), Is.EqualTo(0));

        var broken = projects["Broken/Broken.csproj"];
        Assert.That(broken.GetProperty("status").GetString(), Is.Not.EqualTo("indexed"));
        var reasons = broken.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.That(reasons, Has.Some.StartsWith("restore NU1101:"));
        Assert.That(reasons, Has.Some.Contains("Kb.Fixture.Absent"));
        // The restore error is the cause; the workspace's load failure only repeats it, and no reason
        // carries the machine-specific path of the directory the run happened in.
        Assert.That(reasons, Has.All.StartsWith("restore "));
        Assert.That(reasons, Has.None.Contains(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
    }

    [Test]
    public void ASolutionWhoseProjectsAllRestoreReadsAsIndexedThroughout()
    {
        var outcomes = Index("all-restore", "Clean.slnx");

        var root = outcomes.GetProperty("roots").EnumerateArray().Single();
        Assert.That(root.GetProperty("restore").GetString(), Is.EqualTo("succeeded"));
        var projects = ByProject(outcomes);
        Assert.That(projects.Keys, Is.EquivalentTo(new[] { "First/First.csproj", "Second/Second.csproj" }));
        foreach (var (name, project) in projects)
        {
            Assert.That(project.GetProperty("status").GetString(), Is.EqualTo("indexed"), name);
            Assert.That(project.GetProperty("documents").GetInt32(), Is.GreaterThan(0), name);
        }
    }

    private static Dictionary<string, JsonElement> ByProject(JsonElement outcomes) =>
        outcomes.GetProperty("projects").EnumerateArray()
            .ToDictionary(p => p.GetProperty("project").GetString()!, p => p);

    private static JsonElement Index(string fixture, string solution)
    {
        var source = Path.Join(RootDirectory(), "snapshots", "outcomes", fixture);
        var work = Path.Join(Path.GetTempPath(), "scip-dotnet-outcomes", Guid.NewGuid().ToString("N"));
        CopyDirectory(source, work);
        try
        {
            var framework = $"net{Environment.Version.Major}.0";
            var outcomesPath = Path.Join(work, "outcomes.json");
            var arguments = $"run --project ScipDotnet --framework {framework} -- index \"{Path.Join(work, solution)}\" " +
                            $"--working-directory \"{work}\" --output \"{Path.Join(work, "index.scip")}\" " +
                            $"--project-outcomes \"{outcomesPath}\"";
            using var process = Process.Start(new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = RootDirectory(),
                UseShellExecute = false,
            })!;
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.EqualTo(0), $"dotnet {arguments}");
            using var document = JsonDocument.Parse(File.ReadAllText(outcomesPath));
            return document.RootElement.Clone();
        }
        finally
        {
            try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Join(destination, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Join(destination, Path.GetRelativePath(source, file)));
    }

    private static string RootDirectory()
    {
        using var process = Process.Start(new ProcessStartInfo("git", "rev-parse --show-toplevel")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        return process.StandardOutput.ReadToEnd().Trim();
    }
}
