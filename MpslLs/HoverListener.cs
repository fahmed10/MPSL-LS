using MPSLInterpreter;
using MPSLInterpreter.StdLibrary;
using MpslLs.LspTypes;

namespace MpslLs;

public class HoverListener(CodeVisitor visitor, Token token, Token? prevToken, LspTypes.Range tokenRange, bool used = false) : CodeVisitor.IListener
{
    public Hover? Hover { get; private set; } = null;
    public bool InGroupAccess { get; private set; }
    bool isGroupDeclarationHover = false;
    bool declaringPublic = false;
    bool inGroupBlock = false;
    bool InUsedFile => visitor.InUsedFile || used;
    readonly Dictionary<string, (bool used, Statement.GroupDeclaration group)> groupDeclarations = [];

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
                return Hover == null && declaringPublic;
            }
        }

        if (Hover != null && !isGroupDeclarationHover && node.Start > token.End)
        {
            return false;
        }
        if (node is Expression.GroupAccess access && (token.Start < access.Start || token.End > access.End))
        {
            return false;
        }
        if (node is Expression.Block block && inGroupBlock && prevToken?.Type != TokenType.COLON_COLON && (token.Start < block.Start || token.End > block.End))
        {
            return false;
        }

        return true;
    }

    Hover CreateHover(string text)
    {
        return new(new(MarkupKind.Markdown, $"```mpsl\n{text.TrimEnd()}"), tokenRange);
    }

    void Statement.IVisitor.VisitPublic(Statement.Public statement)
    {
        declaringPublic = true;
    }

    void Expression.IVisitor.VisitVariableDeclaration(Expression.VariableDeclaration expression)
    {
        declaringPublic = false;

        if (expression.name.Lexeme == token.Lexeme)
        {
            Hover = CreateHover($"(variable) {token.Lexeme}");
        }
    }

    void Statement.IVisitor.VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        declaringPublic = false;

        if (!InGroupAccess && prevToken?.Type == TokenType.COLON_COLON)
        {
            return;
        }

        if (statement.name.Lexeme == token.Lexeme)
        {
            Hover = CreateHover($"fn {token.Lexeme} {string.Join(", ", statement.parameters.Select(p => p.Lexeme))}");
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

    void Statement.IVisitor.VisitGroupDeclaration(Statement.GroupDeclaration statement)
    {
        declaringPublic = false;

        if (statement.name.Lexeme == token.Lexeme)
        {
            isGroupDeclarationHover = true;
            Hover = CreateHover($"group {token.Lexeme}");
        }

        groupDeclarations.Add(statement.name.Lexeme, (InUsedFile, statement));
        inGroupBlock = true;
    }

    void CodeVisitor.IListener.GroupDeclarationVisited(Statement.GroupDeclaration groupDeclaration)
    {
        inGroupBlock = false;
    }

    void Statement.IVisitor.VisitUse(Statement.Use statement)
    {
        if (statement.target.Type == TokenType.IDENTIFIER && BuiltInGroups.groups.ContainsKey(statement.target.Lexeme) && statement.target.Lexeme == token.Lexeme)
        {
            Hover = CreateHover($"group {token.Lexeme}");
        }
    }

    void Expression.IVisitor.VisitGroupAccess(Expression.GroupAccess groupAccess)
    {
        InGroupAccess = true;
        string[] groupNames = groupAccess.group.ToString().Split("::");

        if (BuiltInGroups.groups.TryGetValue(groupNames[0], out MPSLGroup? group))
        {
            string fullGroupName = groupNames[0];
            int accessOffset = groupNames[0].Length + 2;

            foreach (string groupName in groupNames[1..])
            {
                if (groupAccess.Start + accessOffset >= token.Start)
                {
                    break;
                }

                try
                {
                    group = group.Environment.GetGroup(groupName);
                }
                catch
                {
                    return;
                }

                accessOffset += groupName.Length + 2;
                fullGroupName += $"::{groupName}";
            }

            if (group.Environment.Variables.Any(name => token.Lexeme == name))
            {
                Hover = CreateHover($"(variable) {fullGroupName}::{token.Lexeme}");
            }
            else if (group.Environment.Functions.Any(name => token.Lexeme == name))
            {
                Hover = CreateHover($"fn {fullGroupName}::{token.Lexeme} {string.Join(", ", group.Environment.GetFunction(token.Lexeme).ParameterNames)}");
            }
            else if (group.Environment.Groups.Any(name => token.Lexeme == name))
            {
                Hover = CreateHover($"group {fullGroupName}::{token.Lexeme}");
            }
        }
        else if (groupDeclarations.TryGetValue(groupNames[0], out (bool used, Statement.GroupDeclaration group) groupDeclaration))
        {
            string fullGroupName = groupNames[0];
            int accessOffset = groupNames[0].Length + 2;

            foreach (string groupName in groupNames[1..])
            {
                if (groupAccess.Start + accessOffset >= token.Start)
                {
                    break;
                }

                try
                {
                    groupDeclaration.group = (Statement.GroupDeclaration)groupDeclaration.group.body.statements.First(s => s is Statement.GroupDeclaration g && g.name.Lexeme == groupName);
                }
                catch
                {
                    return;
                }

                accessOffset += groupName.Length + 2;
                fullGroupName += $"::{groupName}";
            }

            Statement[] accessibleStatements;

            if (groupDeclaration.used)
            {
                accessibleStatements = groupDeclaration.group.body.statements.Where(s => s is Statement.Public).Select(s => ((Statement.Public)s).statement).ToArray();
            }
            else
            {
                accessibleStatements = groupDeclaration.group.body.statements.Select(s => s is Statement.Public p ? p.statement : s).ToArray();
            }

            if (accessibleStatements.Any(s => s is Statement.GroupDeclaration g && g.name.Lexeme == token.Lexeme))
            {
                Hover = CreateHover($"group {fullGroupName}::{token.Lexeme}");
            }
            else if (accessibleStatements.Any(s => s is Statement.FunctionDeclaration f && f.name.Lexeme == token.Lexeme))
            {
                var function = (Statement.FunctionDeclaration)accessibleStatements.First(s => s is Statement.FunctionDeclaration g && g.name.Lexeme == token.Lexeme);
                Hover = CreateHover($"fn {fullGroupName}::{token.Lexeme} {string.Join(", ", function.parameters.Select(p => p.Lexeme))}");
            }
            else if (accessibleStatements.Any(s => s is Statement.ExpressionStatement e && ((e.expression is Expression.VariableDeclaration v && v.name.Lexeme == token.Lexeme) || (e.expression is Expression.Assign a && ((Expression.VariableDeclaration)a.target).name.Lexeme == token.Lexeme))))
            {
                Hover = CreateHover($"(variable) {fullGroupName}::{token.Lexeme}");
            }
        }
    }

    void CodeVisitor.IListener.OnFileVisited()
    {
        if (!InUsedFile && Hover == null && GlobalFunctions.functions.TryGetValue(token.Lexeme.Length > 0 ? token.Lexeme[1..] : token.Lexeme, out NativeFunction? nativeFunction))
        {
            Hover = CreateHover($"fn {token.Lexeme} {string.Join(", ", nativeFunction.Function.Method.GetParameters().Select(p => p.Name))}");
        }
    }
}