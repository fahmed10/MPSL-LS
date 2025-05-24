using MPSLInterpreter;

namespace MpslLs;

// TODO: Add all nodes of AST
public class CodeVisitor : Statement.IVisitor, Expression.IVisitor
{
    public interface IListener : Statement.IVisitor, Expression.IVisitor
    {
        bool ShouldAccept(Statement statement) => true;
        bool ShouldAccept(Expression expression) => true;
        void UseStatementVisited() { }
    }

    IListener currentListener = null!;

    public void Visit(IList<Statement> statements, IListener listener)
    {
        currentListener = listener;

        foreach (Statement statement in statements)
        {
            AcceptIfShould(statement);
        }
    }

    void AcceptIfShould(Statement statement)
    {
        if (currentListener.ShouldAccept(statement))
        {
            statement.Accept(currentListener);
            statement.Accept(this);
        }
    }

    void AcceptIfShould(Expression? expression)
    {
        if (expression != null && currentListener.ShouldAccept(expression))
        {
            expression.Accept(currentListener);
            expression.Accept(this);
        }
    }

    void Expression.IVisitor.VisitBlock(Expression.Block block)
    {
        foreach (Statement statement in block.statements)
        {
            AcceptIfShould(statement);
        }
    }

    void Expression.IVisitor.VisitAssign(Expression.Assign expression)
    {
        if (expression.target is Expression.VariableDeclaration)
        {
            AcceptIfShould(expression.target);
        }
    }

    void Statement.IVisitor.VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        AcceptIfShould(statement.body);
    }

    void Statement.IVisitor.VisitEach(Statement.Each statement)
    {
        AcceptIfShould(statement.body);
    }

    void Statement.IVisitor.VisitIf(Statement.If @if)
    {
        foreach ((_, Expression.Block block) in @if.statements)
        {
            AcceptIfShould(block);
        }

        AcceptIfShould(@if.elseBlock);
    }

    void Statement.IVisitor.VisitWhile(Statement.While statement)
    {
        AcceptIfShould(statement.body);
    }

    void Statement.IVisitor.VisitExpressionStatement(Statement.ExpressionStatement statement)
    {
        AcceptIfShould(statement.expression);
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
        Visit(result.Statements, currentListener);
        currentListener.UseStatementVisited();
    }
}