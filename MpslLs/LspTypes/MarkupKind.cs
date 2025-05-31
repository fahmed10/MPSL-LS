using System.Text.Json.Serialization;

namespace MpslLs.LspTypes;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarkupKind
{
    [JsonStringEnumMemberName("plaintext")]
    Plaintext,
    [JsonStringEnumMemberName("markdown")]
    Markdown
}