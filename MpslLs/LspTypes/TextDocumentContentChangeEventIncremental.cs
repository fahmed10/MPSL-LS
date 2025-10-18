namespace MpslLs.LspTypes;

/// <summary>
/// An event describing an incremental change to a text document.
/// </summary>
/// <param name="Range">The range of the document that changed.</param>
/// <param name="Text">The new text for the provided range.</param>
public record TextDocumentContentChangeEventIncremental(Range Range, string Text);