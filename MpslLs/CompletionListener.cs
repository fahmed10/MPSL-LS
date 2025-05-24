using System.Collections.ObjectModel;
using MPSLInterpreter;
using MpslLs.LspTypes;

namespace MpslLs;

public class CompletionListener(int index) : CodeVisitor.IListener
{
    int useDepth = 0;
    readonly int index = index;
    readonly List<CompletionItem> items = [];
    public ReadOnlyCollection<CompletionItem> Items => items.AsReadOnly();
    public bool InFunctionParameterList { get; private set; }

    bool CodeVisitor.IListener.ShouldAccept(Statement statement)
    {
        if (useDepth > 0)
        {
            return true;
        }

        if (index <= statement.Start)
        {
            return false;
        }
        if (statement is Statement.Each each && (index < each.body.Start || index > each.body.End - 1))
        {
            return false;
        }

        return true;
    }

    bool CodeVisitor.IListener.ShouldAccept(Expression expression)
    {
        if (useDepth > 0)
        {
            return true;
        }

        if (expression is Expression.Block block && (index < block.Start || index > block.End - 1))
        {
            return false;
        }

        return true;
    }

    void Expression.IVisitor.VisitVariableDeclaration(Expression.VariableDeclaration expression)
    {
        items.Add(new(expression.name.Lexeme, CompletionItemKind.Variable));
    }

    void Statement.IVisitor.VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        items.Add(new(statement.name.Lexeme, CompletionItemKind.Function));

        if (index >= statement.body.Start && index <= statement.body.End - 1)
        {
            foreach (Token parameter in statement.parameters)
            {
                items.Add(new(parameter.Lexeme, CompletionItemKind.Variable));
            }
        }

        if (index > statement.name.End && index <= statement.body.Start)
        {
            InFunctionParameterList = true;
        }
    }

    void Statement.IVisitor.VisitEach(Statement.Each statement)
    {
        items.Add(new(statement.variableName.Lexeme, CompletionItemKind.Variable));
    }

    void Statement.IVisitor.VisitUse(Statement.Use useStatement)
    {
        useDepth++;
    }

    void CodeVisitor.IListener.UseStatementVisited()
    {
        useDepth--;
    }
}