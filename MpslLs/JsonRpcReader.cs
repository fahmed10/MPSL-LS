namespace MpslLs;

public class JsonRpcReader(Stream inputStream)
{
    readonly StreamReader reader = new(inputStream);

    private char WaitRead()
    {
        while (true)
        {
            int i = reader.Read();
            if (i >= 0)
            {
                return (char)i;
            }
        }
    }

    public JsonRpcMessage ReadMessage()
    {
        string input = "";
        while (!input.EndsWith("\r\n\r\n"))
        {
            input += WaitRead();
        }

        if (!JsonRpcMessage.TryParseHeader(input, out int contentLength))
        {
            return null;
        }
        input = "";
        for (int i = 0; i < contentLength; i++)
        {
            input += WaitRead();
        }

        if (!JsonRpcMessage.TryParseBody(input, out JsonRpcMessage? message))
        {
            return null;
        }

        return message;
    }
}