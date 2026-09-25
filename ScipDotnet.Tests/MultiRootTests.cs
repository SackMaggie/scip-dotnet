using System.Diagnostics;
using Index = Scip.Index;

namespace ScipDotnet.Tests;

[TestFixture]
public class MultiRootTests
{
    [Test]
    public void TwoSolutionRootsThatShareAProjectIndexItsDocumentsOnce()
    {
        var input = Path.Join(SnapshotTests.RootDirectory(), "snapshots", "input", "syntax");
        var output = Path.Join(Path.GetTempPath(), $"multi-root-{Guid.NewGuid():N}.scip");
        try
        {
            var framework = $"net{Environment.Version.Major}.0";
            var process = Process.Start(new ProcessStartInfo("dotnet",
                $"run --project ScipDotnet --framework {framework} -- index " +
                $"{Path.Join(input, "syntax.sln")} {Path.Join(input, "Main.slnf")} " +
                $"--working-directory {input} --output {output}")
            {
                WorkingDirectory = SnapshotTests.RootDirectory(),
            })!;
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.EqualTo(0));

            var paths = Index.Parser.ParseFrom(File.ReadAllBytes(output)).Documents
                .Select(document => document.RelativePath).ToList();
            Assert.That(paths, Does.Contain(Path.Join("Main", "Classes.cs")),
                "Main is reached by both roots, so its documents must be in the index");
            Assert.That(paths.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty,
                "a document written once per root that reaches it is a duplicate definition to every reader");
        }
        finally
        {
            File.Delete(output);
        }
    }
}
