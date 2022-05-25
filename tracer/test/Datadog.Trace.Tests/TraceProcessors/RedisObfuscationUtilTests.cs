// <copyright file="RedisObfuscationUtilTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Processors;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.TraceProcessors
{
    public class RedisObfuscationUtilTests
    {
        // Test cases from https://github.dev/DataDog/datadog-agent/blob/712c7a7835e0f5aaa47211c4d75a84323eed7fd9/pkg/trace/obfuscate/redis_test.go#L31
        public static TheoryData<string, string> GetRedisQuantizedQuery() => new()
        {
            { "CLIENT", "CLIENT" },
            { "CLIENT LIST", "CLIENT LIST" },
            { "get my_key", "GET" },
            { "SET le_key le_value", "SET" },
            { "\n\n  \nSET foo bar  \n  \n\n  ", "SET" },
            { "CONFIG SET parameter value", "CONFIG SET" },
            { "SET toto tata \n \n  EXPIRE toto 15  ", "SET EXPIRE" },
            { "MSET toto tata toto tata toto tata \n ", "MSET" },
            { "MULTI\nSET k1 v1\nSET k2 v2\nSET k3 v3\nSET k4 v4\nDEL to_del\nEXEC", "MULTI SET SET ..." },
            { "DEL k1\nDEL k2\nHMSET k1 \"a\" 1 \"b\" 2 \"c\" 3\nHMSET k2 \"d\" \"4\" \"e\" \"4\"\nDEL k3\nHMSET k3 \"f\" \"5\"\nDEL k1\nDEL k2\nHMSET k1 \"a\" 1 \"b\" 2 \"c\" 3\nHMSET k2 \"d\" \"4\" \"e\" \"4\"\nDEL k3\nHMSET k3 \"f\" \"5\"\nDEL k1\nDEL k2\nHMSET k1 \"a\" 1 \"b\" 2 \"c\" 3\nHMSET k2 \"d\" \"4\" \"e\" \"4\"\nDEL k3\nHMSET k3 \"f\" \"5\"\nDEL k1\nDEL k2\nHMSET k1 \"a\" 1 \"b\" 2 \"c\" 3\nHMSET k2 \"d\" \"4\" \"e\" \"4\"\nDEL k3\nHMSET k3 \"f\" \"5\"", "DEL DEL HMSET ..." },
            { "GET...", "..." },
            { "GET k...", "GET" },
            { "GET k1\nGET k2\nG...", "GET GET ..." },
            { "GET k1\nGET k2\nDEL k3\nGET k...", "GET GET DEL ..." },
            { "GET k1\nGET k2\nHDEL k3 a\nG...", "GET GET HDEL ..." },
            { "GET k...\nDEL k2\nMS...", "GET DEL ..." },
            { "GET k...\nDE...\nMS...", "GET ..." },
            { "GET k1\nDE...\nGET k2", "GET GET" },
            { "GET k1\nDE...\nGET k2\nHDEL k3 a\nGET k4\nDEL k5", "GET GET HDEL ..." },
            { "UNKNOWN 123", "UNKNOWN" },
            // These are some extra edge cases
            { "  \n \n  ", string.Empty },
            { "GET ...", "GET" },
            { "CLIENT...", "..." },
            { "CLIENT LIST...", "..." },
            { "罿", "罿" },
        };

        public static TheoryData<string, int, int, bool> RedisCompoundCommands() => new()
        {
            { "CLIENT foo", 0, 6, true },
            { "CLUSTER foo", 0, 7, true },
            { "COMMAND foo", 0, 7, true },
            { "CONFIG foo", 0, 6, true },
            { "DEBUG foo", 0, 5, true },
            { "SCRIPT foo", 0, 6, true },
            { "client foo", 0, 6, true },
            { "cluSTER foo", 0, 7, true },
            { "commanD foo", 0, 7, true },
            { "Config foo", 0, 6, true },
            { "debug foo", 0, 5, true },
            { "sCrIpT foo", 0, 6, true },
            { "foo blah SCRIPT baz", 9, 15, true },
            { "CLINT foo", 0, 5, false },
            { "foo CLINT", 4, 9, false },
            { "foo DEBOG", 4, 9, false },
        };

        // Based on https://github.dev/DataDog/datadog-agent/blob/1c76b8381a195a0b0f629011a6225e936fe1d37a/pkg/trace/obfuscate/redis_tokenizer_test.go#L13
        public static TheoryData<string, (string, string, bool)[]> RedisTokenizerData() => new()
        {
            { string.Empty, new[] { (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true) } },
            { "BAD\"\"INPUT\" \"boo\n  Weird13\\Stuff", new[] { ("BAD\"\"INPUT\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"boo\n  Weird13\\Stuff", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "\n  \nCMD\n  \n", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "  CMD  ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD1\nCMD2", new[] { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "  CMD1  \n  CMD2  ", new[] { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD1\nCMD2\nCMD3", new[] { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD1 \n CMD2 \n CMD3 ", new[] { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD arg", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "  CMD  arg  ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 arg2", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { " 	 CMD   arg1 	  arg2 ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1\nCMD2 arg2", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 arg2\nCMD2 arg3\nCMD3\nCMD4 arg4 arg5 arg6", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg5", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg6", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1   arg2  \n CMD2  arg3 \n CMD3 \n  CMD4 arg4 arg5 arg6\nCMD5 ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg5", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg6", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD5", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD \"\"", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD  \"foo bar\"", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD  \"foo bar\\ \" baz", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\\ \"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD \"foo \n bar\" \"\"  baz ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo \n bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("\"\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD \"foo \\\" bar\" baz", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo \\\" bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD  \"foo bar\"  baz", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD \"foo bar\" baz\nCMD2 \"baz\\\\bar\"", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"baz\\\\bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { " CMD  \"foo bar\"  baz \n CMD2  \"baz\\\\bar\"  ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"baz\\\\bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            // These are some extra edge cases
            { "CMD arg1\n CMD2 arg2", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 \nCMD2 arg2", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 \nCMD2 arg2\n", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "\nCMD arg1 \n\nCMD2 arg2\n", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { " \n \n CMD arg1 \n\nCMD2 arg2\n ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 \n\n  \n CMD2 arg2\n ", new[] { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
        };

        [Theory]
        [MemberData(nameof(GetRedisQuantizedQuery))]
        public void RedisQuantizerTest(string inValue, string expectedValue)
        {
            var actualValue = RedisObfuscationUtil.Quantize(inValue);
            actualValue.Should().Be(expectedValue);
        }

        [Theory]
        [MemberData(nameof(RedisCompoundCommands))]
        public void RedisCompoundCommandsTest(string query, int startIndex, int endIndex, bool expected)
        {
            var actual = RedisObfuscationUtil.IsCompoundCommand(query, startIndex, endIndex);
            actual.Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(RedisTokenizerData))]
        public void RedisTokenizerTest(string query, (string Token, string TokenType, bool Done)[] allExpected)
        {
            var tokenizer = new RedisObfuscationUtil.RedisTokenizer(query);
            foreach (var expected in allExpected)
            {
                var done = tokenizer.Scan(out var token);

                using var s = new AssertionScope();
                done.Should().Be(expected.Done);
                token.Offset.Should().BeInRange(0, query.Length);
                (token.Length + token.Offset).Should().BeInRange(0, query.Length);
                token.TokenType.ToString().Should().Be(expected.TokenType);
                var commandArg = query.Substring(token.Offset, token.Length);
                commandArg.Should().Be(expected.Token);
            }
        }
    }
}
