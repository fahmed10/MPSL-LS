using System.Collections.ObjectModel;
using MPSLInterpreter;
using MpslLs.LspTypes;

namespace MpslLs;

public class CompletionListener(CodeVisitor visitor, int index) : CodeVisitor.IListener
{
    readonly List<CompletionItem> items = [];
    public ReadOnlyCollection<CompletionItem> Items => items.AsReadOnly();
    public bool InFunctionParameterList { get; private set; }

    bool CodeVisitor.IListener.ShouldAccept(INode node)
    {
        if (visitor.InUsedFile && node is not Expression.Block)
        {
            return true;
        }

        if (node is Expression.Block block && (index < block.Start || index >= block.End))
        {
            return false;
        }
        if (node is Statement.Each each && (index < each.body.Start || index >= each.body.End))
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
}