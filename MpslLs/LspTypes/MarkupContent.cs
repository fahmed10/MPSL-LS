namespace MpslLs.LspTypes;

/// <summary>
/// <para>
/// A <c>MarkupContent</c> literal represents a string value which content is
/// interpreted based on its kind flag. Currently the protocol supports
/// <c>plaintext</c> and <c>markdown</c> as markup kinds.
/// </para>
/// <para>
/// If the kind is <c>markdown</c> then the value can contain fenced code blocks like
/// in GitHub issues.
/// </para>
/// <para>
/// Please note that clients might sanitize the returned markdown. A client could
/// decide to remove HTML from the markdown to avoid script execution.
/// </para>
/// </summary>
/// <param name="Kind">The type of the Markup.</param>
/// <param name="Value">The content itself.</param>
public record struct MarkupContent(MarkupKind Kind, string Value);