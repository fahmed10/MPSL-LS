namespace MpslLs.LspTypes;

/// <summary>
/// Position in a text document expressed as zero-based line and zero-based character offset. A position is between two characters like an 'insert' cursor in an editor. Special values like for example -1 to denote the end of a line are not supported.
/// </summary>
/// <param name="Line">Line position in a document (zero-based).</param>
/// <param name="Character">Character offset on a line in a document (zero-based). The meaning of this offset is determined by the negotiated <c>PositionEncodingKind</c>. If the character value is greater than the line length it defaults back to the line length.</param>
public record struct Position(int Line, int Character)
{
    public readonly int ToIndexIn(string str)
    {
        int index = 0;

        for (int i = 0; i < Line; i++)
        {
            index = str.IndexOf('\n', index) + 1;
        }

        return index + (int)Character;
    }

    public static Position FromIndexIn(string str, int index)
    {
        int line = 0;
        int column = 0;

        for (int i = 0; i < index; i++)
        {
            column++;

            if (str[i] == '\n')
            {
                line++;
                column = 0;
            }
        }

        return new(line, column);
    }
}