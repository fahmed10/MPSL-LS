using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MPSLInterpreter;
using MPSLInterpreter.StdLibrary;
using MpslLs.LspTypes;

namespace MpslLs;

public class LspServer(Stream outputStream)
{
    static readonly string[] keywords = [.. MPSL.Keywords.Keys];
    static readonly JsonSerializerOptions jsonOptions = JsonSerializerOptions.Web;
    static readonly string[] triggerCharacters = ["@", ":"];
    static readonly CompletionItem[] builtInCompletions = GlobalFunctions.functions.Keys.Select(function => new CompletionItem("@" + function, CompletionItemKind.Function)).ToArray();
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
            else if (method == "textDocument/hover") HandleHover(message);
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
                textDocumentSync = TextDocumentSyncKind.Incremental,
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
        string uri = message.Content.GetPath<string>("params", "textDocument", "uri");
        string oldDocument = documents[uri];
        StringBuilder newDocument = new(oldDocument);

        var changeEvents = message.Content.ParsePathAs<TextDocumentContentChangeEventIncremental[]>(jsonOptions, "params", "contentChanges");
        foreach (var changeEvent in changeEvents)
        {
            int start = changeEvent.Range.Start.ToIndexIn(newDocument.ToString());
            int end = changeEvent.Range.End.ToIndexIn(newDocument.ToString());
            newDocument.Remove(start, end - start);
            newDocument.Insert(start, changeEvent.Text.ReplaceLineEndings("\n"));
        }

        documents[uri] = newDocument.ToString();
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

        CompletionListener completionListener = new(codeVisitor, index);
        codeVisitor.Visit(result.Statements, completionListener);

        bool inString = current?.Type is TokenType.STRING or TokenType.INTERPOLATED_STRING_START or TokenType.INTERPOLATED_STRING_END || currentInclusive?.Type is TokenType.INTERPOLATED_TEXT || (currentInclusive?.Type is TokenType.INTERPOLATED_STRING_START && index > currentInclusive.Start) || (currentInclusive?.Type is TokenType.INTERPOLATED_STRING_END && index < currentInclusive.End);
        bool inComment = current?.Type == TokenType.COMMENT || (currentInclusive?.Type is TokenType.COMMENT && index > currentInclusive.Start && !currentInclusive.Lexeme.StartsWith("##"));
        if (completionListener.InFunctionParameterList || last?.Type is TokenType.VAR or TokenType.EACH or TokenType.FN || inString || inComment)
        {
            SendResponseTo(message, Array.Empty<object>());
            return;
        }

        SendResponseTo(message, ((object[])[
            ..completionListener.Items,
            ..builtInCompletions,
            ..keywords.Select(keyword => new CompletionItem(keyword, CompletionItemKind.Keyword)),
        ]).Distinct());
    }

    void HandleHover(JsonRpcMessage message)
    {
        string text = documents[message.Content.GetPath<string>("params", "textDocument", "uri")];
        Position position = message.Content.ParsePathAs<Position>(jsonOptions, "params", "position");

        MPSLCheckResult result = MPSL.Check(text);
        int index = position.ToIndexIn(text);

        Token? hoverToken = result.Tokens.FirstOrDefault(t => index >= t.Start && index <= t.End);

        if (hoverToken == null)
        {
            SendResponseTo(message, (object?)null);
            return;
        }

        HoverListener hoverListener = new(codeVisitor, hoverToken, new(Position.FromIndexIn(text, hoverToken.Start), Position.FromIndexIn(text, hoverToken.End)));
        codeVisitor.Visit(result.Statements, hoverListener);

        SendResponseTo(message, hoverListener.Hover);
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