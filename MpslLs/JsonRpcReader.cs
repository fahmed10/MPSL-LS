namespace MpslLs;

public class JsonRpcReader(Stream inputStream)
{
    readonly StreamReader reader = new(inputStream);

    public JsonRpcMessage? ReadMessage()
    {
        string input = "";
        while (!input.EndsWith("\r\n\r\n"))
        {
            input += reader.ReadLine() + "\r\n";
        }

        if (!JsonRpcMessage.TryParseHeader(input, out int contentLength))
        {
            reader.ReadToEnd();
            return null;
        }

        char[] buffer = new char[contentLength];
        reader.ReadBlock(buffer, 0, contentLength);

        if (!JsonRpcMessage.TryParseBody(new string(buffer), out JsonRpcMessage? message))
        {
            reader.ReadToEnd();
            return null;
        }

        return message;
    }
}