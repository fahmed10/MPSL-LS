using MPSLInterpreter;
using MPSLInterpreter.StdLibrary;
using MpslLs.LspTypes;

namespace MpslLs;

public class HoverListener(CodeVisitor visitor, Token token, LspTypes.Range tokenRange) : CodeVisitor.IListener
{
    public Hover? Hover { get; private set; } = null;
    bool InUsedFile => visitor.InUsedFile;

    bool CodeVisitor.IListener.ShouldAccept(INode node)
    {
        if (visitor.InUsedFile)
        {
            return Hover == null && node is not Expression.Block;
        }
        if (Hover != null && node.Start > token.End)
        {
            return false;
        }

        return true;
    }

    Hover CreateHover(string text)
    {
        return new(new(MarkupKind.Markdown, $"```mpsl\n{text.TrimEnd()}"), tokenRange);
    }

    void Expression.IVisitor.VisitVariableDeclaration(Expression.VariableDeclaration expression)
    {
        if (expression.name.Lexeme == token.Lexeme)
        {
            Hover = CreateHover($"(variable) {token.Lexeme}");
        }
    }

    void Statement.IVisitor.VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        if (statement.name.Lexeme == token.Lexeme)
        {
            Hover = CreateHover($"{token.Lexeme} {string.Join(", ", statement.parameters.Select(p => p.Lexeme))}");
        }
        else
        {
            foreach (Token parameter in statement.parameters)
            {
                if (parameter.Lexeme == token.Lexeme)
                {
                    Hover = CreateHover($"(parameter) {token.Lexeme}");
                }
            }
        }
    }

    void Statement.IVisitor.VisitEach(Statement.Each statement)
    {
        if (statement.variableName.Lexeme == token.Lexeme)
        {
            Hover = CreateHover($"(variable) {token.Lexeme}");
        }
    }

    void CodeVisitor.IListener.OnFileVisited()
    {
        if (!InUsedFile && Hover == null && GlobalFunctions.functions.TryGetValue(token.Lexeme.Length > 0 ? token.Lexeme[1..] : token.Lexeme, out NativeFunction? nativeFunction))
        {
            Hover = CreateHover($"{token.Lexeme} {string.Join(", ", nativeFunction.Function.Method.GetParameters().Select(p => p.Name))}");
        }
    }
}