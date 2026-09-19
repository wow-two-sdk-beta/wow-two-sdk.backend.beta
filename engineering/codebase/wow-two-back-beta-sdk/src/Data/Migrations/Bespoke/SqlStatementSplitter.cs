using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Splits a SQL script into sequential commands without splitting quoted or commented semicolons.</summary>
internal static class SqlStatementSplitter
{
    /// <summary>Splits <paramref name="script"/> at top-level statement terminators.</summary>
    public static IReadOnlyList<string> Split(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var statements = new List<string>();
        var current = new StringBuilder();
        var dollarQuote = string.Empty;
        var blockCommentDepth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inLineComment = false;

        for (var index = 0; index < script.Length; index++)
        {
            var character = script[index];
            var next = index + 1 < script.Length ? script[index + 1] : '\0';

            if (inLineComment)
            {
                current.Append(character);
                if (character == '\n')
                    inLineComment = false;
                continue;
            }

            if (blockCommentDepth > 0)
            {
                current.Append(character);
                if (character == '/' && next == '*')
                {
                    current.Append(next);
                    index++;
                    blockCommentDepth++;
                }
                else if (character == '*' && next == '/')
                {
                    current.Append(next);
                    index++;
                    blockCommentDepth--;
                }
                continue;
            }

            if (dollarQuote.Length > 0)
            {
                if (script.AsSpan(index).StartsWith(dollarQuote, StringComparison.Ordinal))
                {
                    current.Append(dollarQuote);
                    index += dollarQuote.Length - 1;
                    dollarQuote = string.Empty;
                }
                else
                {
                    current.Append(character);
                }
                continue;
            }

            if (inSingleQuote)
            {
                current.Append(character);
                if (character == '\'' && next == '\'')
                {
                    current.Append(next);
                    index++;
                }
                else if (character == '\'')
                {
                    inSingleQuote = false;
                }
                continue;
            }

            if (inDoubleQuote)
            {
                current.Append(character);
                if (character == '"' && next == '"')
                {
                    current.Append(next);
                    index++;
                }
                else if (character == '"')
                {
                    inDoubleQuote = false;
                }
                continue;
            }

            if (character == '-' && next == '-')
            {
                current.Append(character).Append(next);
                index++;
                inLineComment = true;
                continue;
            }

            if (character == '/' && next == '*')
            {
                current.Append(character).Append(next);
                index++;
                blockCommentDepth = 1;
                continue;
            }

            if (character == '\'')
            {
                current.Append(character);
                inSingleQuote = true;
                continue;
            }

            if (character == '"')
            {
                current.Append(character);
                inDoubleQuote = true;
                continue;
            }

            if (character == '$' && TryReadDollarQuote(script, index, out var delimiter))
            {
                current.Append(delimiter);
                index += delimiter.Length - 1;
                dollarQuote = delimiter;
                continue;
            }

            if (character == ';')
            {
                AddStatement(statements, current);
                continue;
            }

            current.Append(character);
        }

        AddStatement(statements, current);
        return statements;
    }

    private static bool TryReadDollarQuote(string script, int start, out string delimiter)
    {
        var end = start + 1;
        while (end < script.Length && (char.IsLetterOrDigit(script[end]) || script[end] == '_'))
            end++;

        if (end < script.Length && script[end] == '$')
        {
            delimiter = script[start..(end + 1)];
            return true;
        }

        delimiter = string.Empty;
        return false;
    }

    private static void AddStatement(List<string> statements, StringBuilder current)
    {
        var statement = current.ToString().Trim();
        if (statement.Length > 0)
            statements.Add(statement);
        current.Clear();
    }
}
