using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ScipDotnet;

/// <summary>
/// What happened to every project an index run attempted, recorded as the run observed it, so a
/// consumer can tell a complete index from one that silently lacks a project whose restore or load
/// failed. The indexer exits 0 in both cases, which is why this record exists.
/// </summary>
public sealed class ProjectOutcomes
{
    /// <summary>The record's shape version. A consumer refuses a version it does not know.</summary>
    public const int SchemaVersion = 1;

    public const string StatusIndexed = "indexed";
    public const string StatusPartial = "partial";
    public const string StatusNotIndexed = "not-indexed";
    public const string StatusSkipped = "skipped";

    public const string RestoreSucceeded = "succeeded";
    public const string RestoreFailed = "failed";
    public const string RestoreTimedOut = "timed-out";
    public const string RestoreNotRun = "not-run";

    /// <summary>NuGet's own record of a restore, beside the project under its default intermediate
    /// directory. Its <c>logs</c> array carries each restore error with its code.</summary>
    private const string AssetsFile = "obj/project.assets.json";

    private const string ErrorLevel = "Error";

    private readonly List<JsonObject> _roots = new();
    private readonly List<JsonObject> _projects = new();
    private readonly HashSet<string> _recorded = new(StringComparer.OrdinalIgnoreCase);

    public void Root(FileInfo workingDirectory, FileInfo root, string restore, int? restoreExitCode)
    {
        _roots.Add(new JsonObject
        {
            ["path"] = Relative(workingDirectory, root.FullName),
            ["restore"] = restore,
            ["restore_exit_code"] = restoreExitCode,
        });
    }

    public void Project(FileInfo workingDirectory, FileInfo root, string projectPath, string status,
        int documents, IEnumerable<string> reasons)
    {
        if (!_recorded.Add(Path.GetFullPath(projectPath))) return;
        var reasonArray = new JsonArray();
        foreach (var reason in reasons) reasonArray.Add(reason);
        _projects.Add(new JsonObject
        {
            ["root"] = Relative(workingDirectory, root.FullName),
            ["project"] = Relative(workingDirectory, projectPath),
            ["status"] = status,
            ["documents"] = documents,
            ["reasons"] = reasonArray,
        });
    }

    /// <summary>Why a project's restore or load failed, read without interpreting console output:
    /// NuGet's error logs for the project when its restore failed -- the cause, which the workspace's
    /// load failure only repeats -- and otherwise the workspace's load failures that name the
    /// project's file, with that file named relative to the working directory.</summary>
    public static List<string> FailureReasons(FileInfo workingDirectory, string projectPath,
        MSBuildWorkspace workspace)
    {
        var reasons = RestoreErrors(projectPath);
        if (reasons.Count > 0) return reasons;
        var full = Path.GetFullPath(projectPath);
        return workspace.Diagnostics
            .Where(d => d.Kind == WorkspaceDiagnosticKind.Failure
                        && d.Message.Contains(full, StringComparison.OrdinalIgnoreCase))
            .Select(d => "load: " + d.Message.Replace(full, Relative(workingDirectory, full),
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static List<string> RestoreErrors(string projectPath)
    {
        var assets = Path.Join(Path.GetDirectoryName(Path.GetFullPath(projectPath)), AssetsFile);
        if (!File.Exists(assets)) return new List<string>();
        try
        {
            using var stream = File.OpenRead(assets);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("logs", out var logs)
                || logs.ValueKind != JsonValueKind.Array)
                return new List<string>();
            return logs.EnumerateArray()
                .Where(log => log.TryGetProperty("level", out var level)
                              && string.Equals(level.GetString(), ErrorLevel, StringComparison.OrdinalIgnoreCase))
                .Select(log => $"restore {Text(log, "code")}: {Text(log, "message")}")
                .ToList();
        }
        catch (Exception exc) when (exc is IOException or JsonException or UnauthorizedAccessException)
        {
            return new List<string> { $"restore: {assets} could not be read ({exc.Message})" };
        }
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";

    public async Task WriteAsync(FileInfo output)
    {
        var document = new JsonObject
        {
            ["schema_version"] = SchemaVersion,
            ["roots"] = new JsonArray(_roots.Select(r => (JsonNode)r).ToArray()),
            ["projects"] = new JsonArray(_projects.Select(p => (JsonNode)p).ToArray()),
        };
        var directory = Path.GetDirectoryName(output.FullName);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var staging = output.FullName + ".writing";
        await File.WriteAllTextAsync(staging,
            document.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
        File.Move(staging, output.FullName, overwrite: true);
    }

    public int Count(string status) => _projects.Count(p => (string?)p["status"] == status);

    public int Total => _projects.Count;

    private static string Relative(FileInfo workingDirectory, string path) =>
        Path.GetRelativePath(workingDirectory.FullName, path).Replace('\\', '/');
}
