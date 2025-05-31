namespace MpslLs.LspTypes;

/// <summary>
/// A range in a text document expressed as (zero-based) start and end positions. A range is comparable to a selection in an editor. Therefore, the end position is exclusive. If you want to specify a range that contains a line including the line ending character(s) then use an end position denoting the start of the next line.
/// </summary>
/// <param name="Start">The range's start position.</param>
/// <param name="End">The range's end position.</param>
public record struct Range(Position Start, Position End)
{
    public Range(Position position) : this(position, position) { }
}