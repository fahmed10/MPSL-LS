namespace MpslLs.LspTypes;

/// <param name="Label">The label of this completion item. The label property is also by default the text that is inserted when selecting this completion. If label details are provided the label itself should be an unqualified name of the completion item.</param>
/// <param name="Kind">The kind of this completion item. The kind of this completion item. Based of the kind an icon is chosen by the editor. The standardized set of available values is defined in <c>CompletionItemKind</c>.</param>
public record CompletionItem(string Label, CompletionItemKind Kind)
{
    public string FilterText { get; init; } = Label;
    public string InsertText { get; init; } = Label;
}