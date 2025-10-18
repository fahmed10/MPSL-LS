using System.Collections.ObjectModel;
using MPSLInterpreter;
using MPSLInterpreter.StdLibrary;
using MpslLs.LspTypes;

namespace MpslLs;

public class CompletionListener(CodeVisitor visitor, int index, bool used = false) : CodeVisitor.IListener
{
    List<CompletionItem> items = [];
    List<string> groupScope = [];
    readonly Dictionary<string, (bool used, Statement.GroupDeclaration group)> groupDeclarations = [];
    public ReadOnlyCollection<CompletionItem> Items => items.AsReadOnly();
    public bool InFunctionParameterList { get; private set; }
    public bool InGroupAccess { get; private set; }
    bool InUsedFile => visitor.InUsedFile || used;
    int Index => InUsedFile ? int.MaxValue : index;
    bool declaringPublic = false;

    bool CodeVisitor.IListener.ShouldAccept(INode node)
    {
        if (InGroupAccess)
        {
            return false;
        }

        if (InUsedFile)
        {
            if (node is Expression.Block)
            {
                return false;
            }
            if (node is Statement.ExpressionStatement or Statement.FunctionDeclaration or Statement.GroupDeclaration)
            {
                return declaringPublic;
            }
        }

        if (node is Expression.Block block && (Index < block.Start || Index >= block.End + (block.start.Type == TokenType.CURLY_LEFT ? 0 : 1)))
        {
            return false;
        }
        if (node is Statement.Each each && (Index < each.body.Start || (each.body.start.Type == TokenType.CURLY_LEFT && Index >= each.body.End) || (Index > each.body.End)))
        {
            return false;
        }
        if (node is Expression.GroupAccess groupAccess && (Index < groupAccess.Start || Index > groupAccess.End))
        {
            return false;
        }
        if (node is Statement.GroupDeclaration groupDeclaration && Index < groupDeclaration.body.Start)
        {
            return false;
        }

        return true;
    }

    void Statement.IVisitor.VisitPublic(Statement.Public statement)
    {
        declaringPublic = true;
    }

    void Expression.IVisitor.VisitVariableDeclaration(Expression.VariableDeclaration expression)
    {
        items.Add(new(expression.name.Lexeme, CompletionItemKind.Variable));
        declaringPublic = false;
    }

    void Statement.IVisitor.VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        items.Add(new(statement.name.Lexeme, CompletionItemKind.Function));
        declaringPublic = false;

        if (Index >= statement.body.Start && Index <= statement.body.End - (statement.body.start.Type == TokenType.CURLY_LEFT ? 1 : 0))
        {
            foreach (Token parameter in statement.parameters)
            {
                items.Add(new(parameter.Lexeme, CompletionItemKind.Variable));
            }
        }

        if (Index > statement.name.End && Index <= statement.body.Start)
        {
            InFunctionParameterList = true;
        }
    }

    void Statement.IVisitor.VisitEach(Statement.Each statement)
    {
        items.Add(new(statement.variableName.Lexeme, CompletionItemKind.Variable));
    }

    void Statement.IVisitor.VisitGroupDeclaration(Statement.GroupDeclaration groupDeclaration)
    {
        groupDeclarations.Add(groupDeclaration.name.Lexeme, (InUsedFile, groupDeclaration));
        declaringPublic = false;
        groupScope.Add(groupDeclaration.name.Lexeme);
        items.Add(new(groupDeclaration.name.Lexeme, CompletionItemKind.Module));
    }

    void CodeVisitor.IListener.GroupDeclarationVisited(Statement.GroupDeclaration groupDeclaration)
    {
        groupScope.RemoveAt(groupScope.Count - 1);
    }

    void Expression.IVisitor.VisitGroupAccess(Expression.GroupAccess groupAccess)
    {
        InGroupAccess = true;
        string[] groupNames = groupAccess.group.ToString().Split("::");
        List<string> scopedGroupNames = [.. groupScope, .. groupNames];

        if (BuiltInGroups.groups.TryGetValue(groupNames[0], out MPSLGroup? group))
        {
            foreach (string groupName in groupNames[1..])
            {
                try
                {
                    group = group.Environment.GetGroup(groupName);
                }
                catch
                {
                    items = [];
                    return;
                }
            }

            items = [
                ..group.Environment.Variables.Select(name => new CompletionItem(name, CompletionItemKind.Variable)),
                ..group.Environment.Functions.Select(name => new CompletionItem(name, CompletionItemKind.Function)),
                ..group.Environment.Groups.Select(name => new CompletionItem(name, CompletionItemKind.Module)),
            ];
        }
        else
        {
            List<string> currentGroupScope = [.. groupScope];
            scopedGroupNames = [.. currentGroupScope, .. groupNames];
            items = GetGroupCompletionItems(scopedGroupNames);

            while (currentGroupScope.Count > 0 && items.Count == 0)
            {
                currentGroupScope.RemoveAt(currentGroupScope.Count - 1);
                scopedGroupNames = [.. currentGroupScope, .. groupNames];
                items = GetGroupCompletionItems(scopedGroupNames);
            }
        }
    }

    List<CompletionItem> GetGroupCompletionItems(IList<string> groupNames)
    {
        if (!groupDeclarations.TryGetValue(groupNames[0], out (bool used, Statement.GroupDeclaration group) groupDeclaration))
        {
            return [];
        }

        foreach (string groupName in groupNames.Skip(1))
        {
            try
            {
                groupDeclaration.group = (Statement.GroupDeclaration)groupDeclaration.group.body.statements.First(s => s is Statement.GroupDeclaration g && g.name.Lexeme == groupName);
            }
            catch
            {
                return [];
            }
        }

        CodeVisitor codeVisitor = new();
        CompletionListener completionListener = new(codeVisitor, groupDeclaration.group.End, groupDeclaration.used);
        codeVisitor.Visit(visitor.FilePath, groupDeclaration.group.body.statements, completionListener);
        return completionListener.items;
    }

    void CodeVisitor.IListener.UseStatementVisited(Statement.Use useStatement)
    {
        if (useStatement.target.Type == TokenType.IDENTIFIER && BuiltInGroups.groups.ContainsKey(useStatement.target.Lexeme))
        {
            items.Add(new(useStatement.target.Lexeme, CompletionItemKind.Module));
        }
    }
}