using MPSLInterpreter;

namespace MpslLs;

public class CodeVisitor : Statement.IVisitor, Expression.IVisitor
{
    int useDepth = 0;
    public bool InUsedFile => useDepth > 0;
    public string FilePath { get; private set; } = null!;

    private readonly List<string> usedFiles = [];

    public interface IListener : Statement.IVisitor, Expression.IVisitor
    {
        bool ShouldAccept(INode node) => true;
        void UseStatementVisited(Statement.Use useStatement) { }
        void GroupDeclarationVisited(Statement.GroupDeclaration groupDeclaration) { }
        void OnFileVisited() { }
    }

    IListener currentListener = null!;

    public void Visit(string filePath, IList<Statement> statements, IListener listener) => Visit(filePath, statements, listener, true);

    void Visit(string filePath, IList<Statement> statements, IListener listener, bool clearUsed)
    {
        if (clearUsed)
        {
            usedFiles.Clear();
            try
            {
                usedFiles.Add(Path.GetFullPath(filePath));
            }
            catch
            {
                return;
            }
        }

        string previousPath = FilePath;
        FilePath = filePath;
        currentListener = listener;

        foreach (Statement statement in statements)
        {
            AcceptIfShould(statement);
        }

        currentListener.OnFileVisited();
        FilePath = previousPath;
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
        AcceptIfShould(expression.value);

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

    void Statement.IVisitor.VisitGroupDeclaration(Statement.GroupDeclaration statement)
    {
        AcceptIfShould(statement.body);
        currentListener.GroupDeclarationVisited(statement);
    }

    void Statement.IVisitor.VisitUse(Statement.Use useStatement)
    {
        if (useStatement.target.Type == TokenType.IDENTIFIER)
        {
            currentListener.UseStatementVisited(useStatement);
            return;
        }

        string path = Path.Combine(Path.GetDirectoryName(FilePath)!, (string)useStatement.target.Value!);
        string text;

        try
        {
            string fullPath = Path.GetFullPath(path);
            if (usedFiles.Contains(fullPath))
            {
                return;
            }

            usedFiles.Add(fullPath);
            text = File.ReadAllText(path);
        }
        catch
        {
            return;
        }

        MPSLCheckResult result = MPSL.Check(text);
        useDepth++;
        Visit(path, result.Statements, currentListener);
        useDepth--;
        currentListener.UseStatementVisited(useStatement);
    }

    void Statement.IVisitor.VisitPublic(Statement.Public statement)
    {
        AcceptIfShould(statement.statement);
    }

    void Expression.IVisitor.VisitCall(Expression.Call expression)
    {
        AcceptIfShould(expression.callee);
        foreach (Expression argument in expression.arguments)
        {
            AcceptIfShould(argument);
        }
    }

    void Expression.IVisitor.VisitAccess(Expression.Access expression)
    {
        AcceptIfShould(expression.expression);
        AcceptIfShould(expression.indexExpression);
    }

    void Expression.IVisitor.VisitArray(Expression.Array expression)
    {
        foreach ((Expression item, _) in expression.items)
        {
            AcceptIfShould(item);
        }
    }

    void Expression.IVisitor.VisitBinary(Expression.Binary expression)
    {
        AcceptIfShould(expression.left);
        AcceptIfShould(expression.right);
    }

    void Expression.IVisitor.VisitGrouping(Expression.Grouping expression)
    {
        AcceptIfShould(expression.expression);
    }

    void Expression.IVisitor.VisitInterpolatedString(Expression.InterpolatedString expression)
    {
        foreach (Expression expr in expression.expressions)
        {
            AcceptIfShould(expr);
        }
    }

    void Expression.IVisitor.VisitMatch(Expression.Match expression)
    {
        AcceptIfShould(expression.value);
        
        foreach ((Expression condition, Expression.Block body) in expression.statements)
        {
            AcceptIfShould(condition);
            AcceptIfShould(body);
        }

        AcceptIfShould(expression.elseBlock);
    }

    void Expression.IVisitor.VisitObject(Expression.Object expression)
    {
        foreach (Expression.Object.Item item in expression.items)
        {
            if (item is Expression.Object.Item.KeyValue keyValue)
            {
                AcceptIfShould(keyValue.keyExpression);
                AcceptIfShould(keyValue.valueExpression);
            }
            else if (item is Expression.Object.Item.Spread spread)
            {
                AcceptIfShould(spread.valueExpression);
            }
        }
    }

    void Expression.IVisitor.VisitPush(Expression.Push expression)
    {
        AcceptIfShould(expression.value);
    }

    void Expression.IVisitor.VisitUnary(Expression.Unary expression)
    {
        AcceptIfShould(expression.right);
    }
}