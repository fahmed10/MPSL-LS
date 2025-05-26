using System.Text.Json;
using System.Text.Json.Nodes;
using MPSLInterpreter;
using MpslLs.LspTypes;

namespace MpslLs;

public class LspServer(Stream outputStream)
{
    static readonly string[] keywords = ["true", "false", "if", "else", "while", "fn", "var", "break", "match", "each", "null", "use"];
    static readonly JsonSerializerOptions jsonOptions = JsonSerializerOptions.Web;
    static readonly string[] triggerCharacters = ["@"];
    static readonly CompletionItem[] builtInCompletions = BuiltInFunctions.functions.Select(pair => new CompletionItem("@" + pair.Key, CompletionItemKind.Function)).ToArray();
    readonly StreamWriter writer = new(outputStream);
    readonly Dictionary<string, string> documents = [];
    readonly CodeVisitor codeVisitor = new();

    public void ProcessMessage(JsonRpcMessage message)
    {
        Debug.Log($"IN: {message.ContentString}");

        try
        {
            string method = message.Content["method"]!.GetValue<string>();

            if (method == "initialize") HandleInitialize(message);
            else if (method == "textDocument/completion") HandleCompletion(message);
            else if (method == "textDocument/diagnostic") HandleDiagnostic(message);
            else if (method == "textDocument/didOpen") HandleDocumentOpen(message);
            else if (method == "textDocument/didClose") HandleDocumentClose(message);
            else if (method == "textDocument/didChange") HandleDocumentChange(message);
        }
        catch (Exception e)
        {
            SendErrorResponseTo(message, new { code = -32603, e.Message, data = new { e.StackTrace } });
        }
    }

    void HandleInitialize(JsonRpcMessage message)
    {
        SendResponseTo(message, new
        {
            capabilities = new
            {
                textDocumentSync = 1,
                completionProvider = new { triggerCharacters },
                diagnosticProvider = new
                {
                    interFileDependencies = true,
                    workspaceDiagnostics = false
                },
                hoverProvider = new { },
            },
            serverInfo = new
            {
                name = "MPSL Language Server",
                version = "1.0.0"
            }
        });
    }

    void HandleDocumentOpen(JsonRpcMessage message)
    {
        JsonNode documentNode = message.Content["params"]!["textDocument"]!;
        documents.Add(documentNode.GetPath<string>("uri"), documentNode.GetPath<string>("text").ReplaceLineEndings("\n"));
    }

    void HandleDocumentClose(JsonRpcMessage message)
    {
        JsonNode documentNode = message.Content["params"]!["textDocument"]!;
        documents.Remove(documentNode.GetPath<string>("uri"));
    }

    void HandleDocumentChange(JsonRpcMessage message)
    {
        JsonNode documentNode = message.Content["params"]!["textDocument"]!;
        documents[documentNode.GetPath<string>("uri")] = message.Content["params"]!["contentChanges"]![0]!["text"]!.GetValue<string>().ReplaceLineEndings("\n");
    }

    void HandleCompletion(JsonRpcMessage message)
    {
        string text = documents[message.Content.GetPath<string>("params", "textDocument", "uri")];
        Position position = message.Content.ParsePathAs<Position>(jsonOptions, "params", "position");

        MPSLCheckResult result = MPSL.Check(text);
        int index = position.ToIndexIn(text);

        Token? current = result.Tokens.FirstOrDefault(t => index > t.Start && index < t.End);
        Token? currentInclusive = result.Tokens.FirstOrDefault(t => index >= t.Start && index <= t.End);
        Token? last = result.Tokens.LastOrDefault(t => index > t.End);

        CompletionListener completionListener = new(index);
        codeVisitor.Visit(result.Statements, completionListener);

        bool inString = current?.Type is TokenType.STRING or TokenType.INTERPOLATED_STRING_MARKER || currentInclusive?.Type is TokenType.INTERPOLATED_TEXT || (currentInclusive?.Type is TokenType.INTERPOLATED_STRING_MARKER && index > currentInclusive.Start && currentInclusive.Lexeme is "@\"");
        bool inComment = current?.Type == TokenType.COMMENT || (currentInclusive?.Type is TokenType.COMMENT && index > currentInclusive.Start && !currentInclusive.Lexeme.StartsWith("##"));
        if (completionListener.InFunctionParameterList || last?.Type is TokenType.VAR or TokenType.EACH or TokenType.FN || inString || inComment)
        {
            SendResponseTo(message, Array.Empty<object>());
            return;
        }

        SendResponseTo(message, (object[])[
            ..completionListener.Items,
            ..builtInCompletions,
            ..keywords.Select(keyword => new CompletionItem(keyword, CompletionItemKind.Keyword)),
        ]);
    }

    void HandleDiagnostic(JsonRpcMessage message)
    {
        string text = documents[message.Content.GetPath<string>("params", "textDocument", "uri")];
        MPSLCheckResult result = MPSL.Check(text);

        var Error = (LspTypes.Range range, string message) => new { range, message, severity = DiagnosticSeverity.Error, source = "mpsl" };

        (int Line, int Column, string Message)[] errors = result.TokenizerErrors.Select(e => (e.Line, e.Column, e.Message))
            .Concat(result.ParserErrors.Select(e => (e.Token.Line, e.Token.Column, e.Message)))
            .ToArray();

        SendResponseTo(message, new
        {
            kind = "full",
            items = errors.Select(e => Error(new(new(e.Line - 1, e.Column)), e.Message)).ToArray()
        });
    }

    void SendResponseTo<T>(JsonRpcMessage request, T content)
    {
        string contentString = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = request.Content["id"]?.GetValue<int>(),
            result = content
        }, jsonOptions);

        JsonRpcMessage message = JsonRpcMessage.ParseBody(contentString);
        Debug.Log($"\tOUT: {message.ContentString}");
        writer.Write(message.Serialize());
        writer.Flush();
    }

    void SendErrorResponseTo<T>(JsonRpcMessage request, T content)
    {
        string contentString = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = request.Content["id"]?.GetValue<int>(),
            error = content
        }, jsonOptions);

        JsonRpcMessage message = JsonRpcMessage.ParseBody(contentString);
        Debug.Log($"\tOUT! {message.ContentString}");
        writer.Write(message.Serialize());
        writer.Flush();
    }
}