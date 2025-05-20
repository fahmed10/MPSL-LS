using System.Text.Json;
using System.Text.Json.Nodes;

namespace MpslLsp;

public static class Utils
{
    public static T GetPath<T>(this JsonNode node, params string[] path)
    {
        JsonNode currentNode = node;
        foreach (string str in path)
        {
            currentNode = currentNode[str]!;
        }
        return currentNode.GetValue<T>();
    }

    public static T ParsePathAs<T>(this JsonNode node, JsonSerializerOptions options, params string[] path)
    {
        JsonNode currentNode = node;
        foreach (string str in path)
        {
            currentNode = currentNode[str]!;
        }
        return JsonSerializer.Deserialize<T>(currentNode.ToJsonString(), options)!;
    }
}