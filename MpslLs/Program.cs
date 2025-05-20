using System.Collections.Concurrent;

namespace MpslLsp;

public class Program
{
    static readonly ConcurrentQueue<JsonRpcMessage> queue = new();

    static int Main(string[] args)
    {
        if (args.Contains("--pipe"))
        {
            Console.WriteLine("Communication through pipes is not supported yet.");
            return 1;
        }

        if (args.Contains("--debug"))
        {
            Debug.DebugMode = true;
            Debug.Initialize();
        }

        Debug.Log("INFO: LSP Started");
        Start(Console.OpenStandardInput(), Console.OpenStandardOutput());
        return 0;
    }

    public static void Start(Stream input, Stream output)
    {
        Task.Run(() => RunServer(output));
        JsonRpcReader reader = new(input);

        while (true)
        {
            JsonRpcMessage message = reader.ReadMessage();
            queue.Enqueue(message);
        }
    }

    static async Task RunServer(Stream output)
    {
        LspServer server = new(output);

        while (true)
        {
            if (queue.TryDequeue(out JsonRpcMessage? message))
            {
                server.ProcessMessage(message);
            }

            await Task.Delay(5);
        }
    }
}
