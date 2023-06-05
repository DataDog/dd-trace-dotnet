// <copyright file="SqlInjectionTokenizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

/// <summary>
/// Tokenizer for SQL_INJECTION vulnerability
/// It locates all the parameter literals in a SQLQuery
/// SELECT id FROM users WHERE location = ‘Paris’ and ZipCode = 324502 -> SELECT id FROM users WHERE location = ‘?’ and ZipCode = ?
/// </summary>
internal class SqlInjectionTokenizer : ITokenizer
{
    private const string StringLiteral = "'(?:''|[^'])*'";
    private const string OracleEscapedLiteral = "q'<.*?>'|q'\\(.*?\\)'|q'\\{.*?\\}'|q'\\[.*?\\]'|q'(?<ESCAPE>.).*?\\k<ESCAPE>'";
    private const string PostgresqlEscapedLiteral = "\\$([^$]*)\\$.*?\\$\\1\\$";
    private const string MysqlStringLiteral = "\"(?:\\\"|[^\"])*\"|'(?:(?:'')|[^'])*'";
    private const string LineComment = "--.*$";
    private const string BlockComment = "/\\*[\\s\\S]*\\*/";
    private const string Exponent = "(?:E[-+]?\\d+[fd]?)?";
    private const string IntegerNumber = "(?<!\\w)\\d+";
    private const string DecimalNumber = "\\d*\\.\\d+";
    private const string HexNumber = "x'[0-9a-f]+'|0x[0-9a-f]+";
    private const string BinNumber = "b'[0-9a-f]+'|0b[0-9a-f]+";
    private static string numericLiteral = $"[-+]?(?:{string.Join("|", HexNumber, BinNumber, DecimalNumber + Exponent, IntegerNumber + Exponent)})";

    private static Regex _ansiDialectPattern = new Regex(string.Join("|", numericLiteral, StringLiteral, LineComment, BlockComment), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex _oracleDialectPattern = new Regex(string.Join("|", numericLiteral, OracleEscapedLiteral, StringLiteral, LineComment, BlockComment), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex _postgresqlDialectPattern = new Regex(string.Join("|", numericLiteral, PostgresqlEscapedLiteral, StringLiteral, LineComment, BlockComment), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex _mySqlDialectPattern = new Regex(string.Join("|", numericLiteral, MysqlStringLiteral, StringLiteral, LineComment, BlockComment), RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static Dictionary<IntegrationId, Regex> _dialectPatterns = new Dictionary<IntegrationId, Regex>
    {
        { IntegrationId.Oracle, _oracleDialectPattern },
        { IntegrationId.Npgsql, _postgresqlDialectPattern },
        { IntegrationId.MySql, _mySqlDialectPattern },
        { IntegrationId.Sqlite, _mySqlDialectPattern }
    };

    public List<Range> GetTokens(string value, IntegrationId? integrationId = null)
    {
        Regex? pattern = null;
        if (integrationId != null) { _dialectPatterns.TryGetValue(integrationId.Value, out pattern); }
        if (pattern == null) { pattern = _ansiDialectPattern; }

        var res = new List<Range>(5);
        var matches = pattern.Matches(value);
        foreach (Match? match in matches)
        {
            if (match == null || !match.Success) { continue; }
            int start = match.Index;
            int end = match.Index + match.Length;
            char startChar = value[start];
            if (startChar == '\'' || startChar == '"')
            {
                start++;
                end--;
            }
            else if (end > start + 1)
            {
                char nextChar = value[start + 1];
                if (startChar == '/' && nextChar == '*')
                {
                    start += 2;
                    end -= 2;
                }
                else if (startChar == '-' && startChar == nextChar)
                {
                    start += 2;
                }
                else if (char.ToLower(startChar) == 'q' && nextChar == '\'')
                {
                    start += 3;
                    end -= 2;
                }
                else if (startChar == '$')
                {
                    var matchValue = match.Value;
                    int size = matchValue.IndexOf('$', 1) + 1;
                    if (size > 1)
                    {
                        start += size;
                        end -= size;
                    }
                }
            }

            res.Add(new Range(start, end - start, null));
        }

        return res;
    }
}
