using System.Diagnostics;
using System.Reflection;
using Index = Scip.Index;

namespace ScipDotnet.Tests;

[TestFixture]
public class ToolInfoTests
{
    [Test]
    public void AnIndexNamesTheBuildThatWroteItAsItsVersionAnswersIt()
    {
        var input = Path.Join(SnapshotTests.RootDirectory(), "snapshots", "input", "syntax");
        var output = Path.Join(Path.GetTempPath(), $"tool-info-{Guid.NewGuid():N}.scip");
        try
        {
            var framework = $"net{Environment.Version.Major}.0";
            var process = Process.Start(new ProcessStartInfo("dotnet",
                $"run --project ScipDotnet --framework {framework} -- index {Path.Join(input, "syntax.sln")} " +
                $"--working-directory {input} --output {output}")
            {
                WorkingDirectory = SnapshotTests.RootDirectory(),
            })!;
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.EqualTo(0));

            var answer = typeof(IndexCommandHandler).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            var tool = Index.Parser.ParseFrom(File.ReadAllBytes(output)).Metadata.ToolInfo;
            Assert.That(tool.Name, Is.EqualTo("scip-dotnet"));
            Assert.That(tool.Version, Is.EqualTo(answer),
                "an index a reader cannot attribute to the build that wrote it cannot be told from any other build's");
        }
        finally
        {
            File.Delete(output);
        }
    }
}
