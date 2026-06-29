// <copyright file="SqlQueryParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util
{
    internal static class SqlQueryParser
    {
        internal static ParseResult Parse(string? commandText)
        {
            if (StringUtil.IsNullOrEmpty(commandText))
            {
                return new ParseResult(null, null);
            }

            var pos = SkipLeadingBlockComments(commandText, 0);
            var verb = ReadToken(commandText, pos, out pos);
            if (verb is null)
            {
                return new ParseResult(null, null);
            }

            var operation = GetOperation(verb);
            if (operation is null)
            {
                return new ParseResult(null, null);
            }

            var table = ExtractTable(commandText, operation);
            return new ParseResult(operation, table);
        }

        private static string? GetOperation(string verb)
        {
            if (verb.Equals("SELECT", StringComparison.OrdinalIgnoreCase)) { return "SELECT"; }

            if (verb.Equals("INSERT", StringComparison.OrdinalIgnoreCase)) { return "INSERT"; }

            if (verb.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)) { return "UPDATE"; }

            if (verb.Equals("DELETE", StringComparison.OrdinalIgnoreCase)) { return "DELETE"; }

            if (verb.Equals("CREATE", StringComparison.OrdinalIgnoreCase)) { return "CREATE"; }

            if (verb.Equals("DROP", StringComparison.OrdinalIgnoreCase)) { return "DROP"; }

            if (verb.Equals("ALTER", StringComparison.OrdinalIgnoreCase)) { return "ALTER"; }

            if (verb.Equals("MERGE", StringComparison.OrdinalIgnoreCase)) { return "MERGE"; }

            if (verb.Equals("CALL", StringComparison.OrdinalIgnoreCase)) { return "CALL"; }

            if (verb.Equals("EXEC", StringComparison.OrdinalIgnoreCase)) { return "EXEC"; }

            if (verb.Equals("EXECUTE", StringComparison.OrdinalIgnoreCase)) { return "EXECUTE"; }

            if (verb.Equals("TRUNCATE", StringComparison.OrdinalIgnoreCase)) { return "TRUNCATE"; }

            return null;
        }

        private static string? ExtractTable(string text, string operation)
        {
            switch (operation)
            {
                case "SELECT":
                case "DELETE":
                    return ExtractTableAfterFrom(text);
                case "INSERT":
                    return ExtractTableAfterInto(text);
                case "UPDATE":
                    return ExtractTableAfterUpdate(text);
                default:
                    return null;
            }
        }

        private static string? ExtractTableAfterFrom(string text)
        {
            var pos = 0;
            while (true)
            {
                var token = ReadToken(text, pos, out var nextPos);
                if (token is null)
                {
                    return null;
                }

                if (token.Equals("FROM", StringComparison.OrdinalIgnoreCase))
                {
                    pos = nextPos;
                    break;
                }

                pos = nextPos;
            }

            // subquery check
            var peek = SkipWhitespace(text, pos);
            if (peek < text.Length && text[peek] == '(')
            {
                return null;
            }

            var tableToken = ReadTableIdentifier(text, pos, out var afterTable);
            if (tableToken is null)
            {
                return null;
            }

            // multi-table check: comma after table
            var afterPeek = SkipWhitespace(text, afterTable);
            if (afterPeek < text.Length && text[afterPeek] == ',')
            {
                return null;
            }

            // JOIN check: if the next token (or the one after an alias) is a JOIN keyword, multiple tables are involved
            var nextToken = ReadToken(text, afterTable, out var afterNext);
            if (nextToken is not null && IsJoinKeyword(nextToken))
            {
                return null;
            }

            // Also check one more token ahead to handle optional table alias: FROM users u JOIN ...
            if (nextToken is not null)
            {
                var tokenAfterAlias = ReadToken(text, afterNext, out _);
                if (tokenAfterAlias is not null && IsJoinKeyword(tokenAfterAlias))
                {
                    return null;
                }
            }

            return NormalizeIdentifier(tableToken);
        }

        private static bool IsJoinKeyword(string token)
        {
            return token.Equals("JOIN", StringComparison.OrdinalIgnoreCase)
                || token.Equals("INNER", StringComparison.OrdinalIgnoreCase)
                || token.Equals("LEFT", StringComparison.OrdinalIgnoreCase)
                || token.Equals("RIGHT", StringComparison.OrdinalIgnoreCase)
                || token.Equals("CROSS", StringComparison.OrdinalIgnoreCase)
                || token.Equals("FULL", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtractTableAfterInto(string text)
        {
            // Known limitation: string literals in SQL are not handled. Tokens containing
            // the word INTO inside a quoted string are unlikely but not excluded by the tokenizer.
            var pos = 0;
            while (true)
            {
                var token = ReadToken(text, pos, out var nextPos);
                if (token is null)
                {
                    return null;
                }

                if (token.Equals("INTO", StringComparison.OrdinalIgnoreCase))
                {
                    pos = nextPos;
                    break;
                }

                pos = nextPos;
            }

            var tableToken = ReadTableIdentifier(text, pos, out _);
            return tableToken is null ? null : NormalizeIdentifier(tableToken);
        }

        private static string? ExtractTableAfterUpdate(string text)
        {
            // skip UPDATE keyword
            ReadToken(text, 0, out var pos);
            var tableToken = ReadTableIdentifier(text, pos, out _);
            return tableToken is null ? null : NormalizeIdentifier(tableToken);
        }

        private static string? ReadTableIdentifier(string text, int pos, out int nextPos)
        {
            var lastToken = ReadToken(text, pos, out pos);
            if (lastToken is null)
            {
                nextPos = pos;
                return null;
            }

            // Handle [schema].[table] or "schema"."table": if next non-whitespace is '.', keep advancing
            while (true)
            {
                var peek = SkipWhitespace(text, pos);
                if (peek >= text.Length || text[peek] != '.')
                {
                    break;
                }

                var next = ReadToken(text, peek + 1, out var afterNext);
                if (next is null)
                {
                    break;
                }

                lastToken = next;
                pos = afterNext;
            }

            nextPos = pos;
            return lastToken;
        }

        private static string? NormalizeIdentifier(string token)
        {
            if (StringUtil.IsNullOrEmpty(token))
            {
                return null;
            }

            // Strip surrounding quote characters: "...", `...`, [...]
            if (token.Length >= 2)
            {
                char first = token[0];
                char last = token[token.Length - 1];
                if ((first == '"' && last == '"') ||
                    (first == '`' && last == '`') ||
                    (first == '[' && last == ']'))
                {
                    token = token.Substring(1, token.Length - 2);
                }
            }

            // Strip schema prefix from unquoted names: schema.table → table
            var dotIndex = token.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < token.Length - 1)
            {
                token = token.Substring(dotIndex + 1);
            }

            return StringUtil.IsNullOrEmpty(token) ? null : token;
        }

        private static int SkipWhitespaceAndComments(string text, int pos)
        {
            while (true)
            {
                pos = SkipWhitespace(text, pos);
                if (pos + 1 < text.Length && text[pos] == '/' && text[pos + 1] == '*')
                {
                    var end = text.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        return text.Length;
                    }

                    pos = end + 2;
                }
                else
                {
                    break;
                }
            }

            return pos;
        }

        private static int SkipLeadingBlockComments(string text, int pos)
        {
            return SkipWhitespaceAndComments(text, pos);
        }

        private static int SkipWhitespace(string text, int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            {
                pos++;
            }

            return pos;
        }

        private static string? ReadToken(string text, int pos, out int nextPos)
        {
            // Skip whitespace and block comments before reading the token
            pos = SkipWhitespaceAndComments(text, pos);
            if (pos >= text.Length)
            {
                nextPos = pos;
                return null;
            }

            char first = text[pos];

            // Quoted identifier: read until closing quote
            char? closingQuote = first switch
            {
                '"' => '"',
                '`' => '`',
                '[' => ']',
                _ => null
            };

            if (closingQuote.HasValue)
            {
                var end = text.IndexOf(closingQuote.Value, pos + 1);
                if (end < 0)
                {
                    end = text.Length - 1;
                }

                nextPos = end + 1;
                return text.Substring(pos, nextPos - pos);
            }

            // Regular token: read until whitespace or delimiter
            var start = pos;
            while (pos < text.Length &&
                   !char.IsWhiteSpace(text[pos]) &&
                   text[pos] != ',' &&
                   text[pos] != ';' &&
                   text[pos] != '(' &&
                   text[pos] != ')')
            {
                pos++;
            }

            nextPos = pos;
            var token = text.Substring(start, pos - start);
            return StringUtil.IsNullOrEmpty(token) ? null : token;
        }

        internal readonly struct ParseResult
        {
            internal readonly string? Operation;
            internal readonly string? Table;

            internal ParseResult(string? operation, string? table)
            {
                Operation = operation;
                Table = table;
            }

            internal void Deconstruct(out string? operation, out string? table)
            {
                operation = Operation;
                table = Table;
            }
        }
    }
}
