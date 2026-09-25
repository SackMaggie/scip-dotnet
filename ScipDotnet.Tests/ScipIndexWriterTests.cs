using Google.Protobuf;
using Scip;
using Index = Scip.Index;

namespace ScipDotnet.Tests;

[TestFixture]
public class ScipIndexWriterTests
{
    private static readonly Metadata Header = new()
    {
        ProjectRoot = "file:///repo",
        ToolInfo = new ToolInfo { Name = "scip-dotnet" },
        TextDocumentEncoding = TextEncoding.Utf8,
    };

    private static Document Doc(string path) => new()
    {
        RelativePath = path,
        Language = "C#",
        Occurrences = { new Occurrence { Symbol = $"local {path}", Range = { 0, 0, 5 } } },
    };

    private static async IAsyncEnumerable<Document> Produce(params Document[] documents)
    {
        foreach (var document in documents)
        {
            await Task.Yield();
            yield return document;
        }
    }

    [Test]
    public async Task EachDocumentReachesTheOutputBeforeTheNextIsProduced()
    {
        using var output = new MemoryStream();
        var lengthWhenAsked = new List<long>();

        async IAsyncEnumerable<Document> Watched()
        {
            foreach (var path in new[] { "a.cs", "b.cs", "c.cs" })
            {
                lengthWhenAsked.Add(output.Length);
                await Task.Yield();
                yield return Doc(path);
            }
        }

        await IndexCommandHandler.WriteIndexAsync(output, Header, Watched());

        Assert.That(lengthWhenAsked, Is.Ordered.Ascending.And.Unique,
            "an index held in memory until the last document writes nothing while the documents are produced");
    }

    [Test]
    public async Task TheStreamedIndexIsByteForByteTheIndexItDescribes()
    {
        using var output = new MemoryStream();
        var documents = new[] { Doc("a.cs"), Doc("b.cs") };

        var written = await IndexCommandHandler.WriteIndexAsync(output, Header, Produce(documents));

        Assert.That(written, Is.EqualTo(2));
        Assert.That(output.ToArray(), Is.EqualTo(new Index { Metadata = Header, Documents = { documents } }.ToByteArray()));
    }

    [Test]
    public void ARunThatFailsMidwayLeavesNoFileBehind()
    {
        var directory = Directory.CreateTempSubdirectory("scip-writer-");
        try
        {
            async IAsyncEnumerable<Document> DiesAfterOne()
            {
                yield return Doc("a.cs");
                await Task.Yield();
                throw new InvalidOperationException("the indexer died");
            }

            var output = new FileInfo(Path.Join(directory.FullName, "index.scip"));
            Assert.ThrowsAsync<InvalidOperationException>(
                () => IndexCommandHandler.WriteIndexFileAsync(output, Header, DiesAfterOne()));
            Assert.That(directory.GetFiles(), Is.Empty);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
