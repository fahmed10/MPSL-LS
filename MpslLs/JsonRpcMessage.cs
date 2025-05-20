using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MpslLsp;

public class JsonRpcMessage
{
    public int ContentLength { get; private set; }
    public string ContentString { get; private set; } = null!;
    public JsonNode Content { get; private set; } = null!;

    JsonRpcMessage() { }

    public string Serialize()
    {
        return $"Content-Length: {ContentLength}\r\n\r\n{ContentString}";
    }

    public static bool TryParseBody(string content, [NotNullWhen(true)] out JsonRpcMessage? message)
    {
        try
        {
            message = ParseBody(content);
            return true;
        }
        catch (JsonException)
        {
            message = null;
            return false;
        }
    }

    public static JsonRpcMessage ParseBody(string content)
    {
        return new()
        {
            ContentLength = content.Length,
            ContentString = content,
            Content = JsonNode.Parse(content)!
        };
    }

    public static bool TryParseHeader(string str, out int contentLength)
    {
        contentLength = 0;

        foreach (string header in str.Split("\r\n"))
        {
            string[] headerParts = str.Split(":");
            if (headerParts.Length != 2)
            {
                return false;
            }

            if (headerParts[0] == "Content-Length")
            {
                if (!int.TryParse(headerParts[1], out contentLength))
                {
                    return false;
                }
            }
            else if (headerParts[0] == "Content-Type")
            {
                if (!(headerParts[1] is "application/vscode-jsonrpc; charset=utf-8" or "application/vscode-jsonrpc; charset=utf8"))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return contentLength > 0;
    }
}