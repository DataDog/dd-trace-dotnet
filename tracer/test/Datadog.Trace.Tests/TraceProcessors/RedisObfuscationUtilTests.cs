// <copyright file="RedisObfuscationUtilTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Processors;
using FluentAssertions;
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
    }
}
