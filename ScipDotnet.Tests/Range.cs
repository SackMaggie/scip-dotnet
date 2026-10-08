using Scip;

namespace ScipDotnet.Tests;

public record Range(Position Start, Position End) : IComparable<Range>
{
    public int CompareTo(Range? other)
    {
        if (other == null)
        {
            return 1;
        }

        var byStart = Start.CompareTo(other.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        return End.CompareTo(other.End);
    }

    public static Range FromOccurrence(Occurrence occ) => FromRaw(occ.Range);

    // SCIP's range encoding, shared by `range` and `enclosing_range`: [line, start, end] on one line,
    // [startLine, startCharacter, endLine, endCharacter] across lines.
    public static Range FromRaw(IReadOnlyList<int> range)
    {
        if (range.Count == 3)
            return new Range(new Position(range[0], range[1]), new Position(range[0], range[2]));

        if (range.Count == 4)
            return new Range(new Position(range[0], range[1]), new Position(range[2], range[3]));

        throw new ArgumentException($"a range must have length 3 or length 4, obtained {range}");
    }
}