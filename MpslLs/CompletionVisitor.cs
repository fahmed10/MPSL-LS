using MPSLInterpreter;
using MpslLsp.LspTypes;

namespace MpslLsp;

public class CompletionVisitor : Statement.IVisitor, Expression.IVisitor
{
    int index;
    readonly List<CompletionItem> items = [];

    public IList<CompletionItem> Check(IList<Statement> statements, int index)
    {
        items.Clear();
        this.index = index;

        foreach (Statement statement in statements)
        {
            AcceptIfAfter(statement);
        }

        return items;
    }

    void AcceptIfAfter(Statement statement)
    {
        if (index > statement.Start)
        {
            statement.Accept(this);
        }
    }

    void Expression.IVisitor.VisitBlock(Expression.Block block)
    {
        if (index < block.Start || index > block.End - 1)
        {
            return;
        }

        foreach (Statement statement in block.statements)
        {
            AcceptIfAfter(statement);
        }
    }

    void Expression.IVisitor.VisitVariableDeclaration(Expression.VariableDeclaration expression)
    {
        items.Add(new(expression.name.Lexeme, CompletionItemKind.Variable));
    }

    void Expression.IVisitor.VisitAssign(Expression.Assign expression)
    {
        if (expression.target is Expression.VariableDeclaration)
        {
            expression.target.Accept(this);
        }
    }

    void Statement.IVisitor.VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        items.Add(new(statement.name.Lexeme, CompletionItemKind.Function));

        statement.body.Accept(this);
    }

    void Statement.IVisitor.VisitEach(Statement.Each statement)
    {
        if (index < statement.body.Start || index > statement.body.End - 1)
        {
            return;
        }

        items.Add(new(statement.variableName.Lexeme, CompletionItemKind.Variable));
        statement.body.Accept(this);
    }

    void Statement.IVisitor.VisitIf(Statement.If @if)
    {
        foreach ((_, Expression.Block block) in @if.statements)
        {
            block.Accept(this);
        }

        @if.elseBlock?.Accept(this);
    }

    void Statement.IVisitor.VisitWhile(Statement.While statement)
    {
        
        statement.body.Accept(this);
    }

    void Statement.IVisitor.VisitExpressionStatement(Statement.ExpressionStatement statement)
    {
        statement.expression.Accept(this);
    }

    void Statement.IVisitor.VisitUse(Statement.Use useStatement)
    {
        string path = (string)useStatement.path.Value!;
        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch { return; }

        MPSLCheckResult result = MPSL.Check(text);
        CompletionVisitor visitor = new();
        items.AddRange(visitor.Check(result.Statements, text.Length));
    }
}