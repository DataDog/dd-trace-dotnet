// <copyright file="RedisObfuscationUtilTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Processors;
using Datadog.Trace.TestHelpers;
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
            { string.Empty, string.Empty },
            { null, string.Empty },
            { "CONFIG SET parameter\t\t value\t\nCONFIG SET parameter\t\t value\t value \n", "CONFIG SET CONFIG SET" },
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
        public static TheoryData<string, SerializableList<(string Token, string TokenType, bool Done)>> RedisTokenizerData() => new()
        {
            { string.Empty, new() { (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true) } },
            { "BAD\"\"INPUT\" \"boo\n  Weird13\\Stuff", new() { ("BAD\"\"INPUT\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"boo\n  Weird13\\Stuff", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "\n  \nCMD\n  \n", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "  CMD  ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD1\nCMD2", new() { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "  CMD1  \n  CMD2  ", new() { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD1\nCMD2\nCMD3", new() { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD1 \n CMD2 \n CMD3 ", new() { ("CMD1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD arg", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "  CMD  arg  ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 arg2", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { " 	 CMD   arg1 	  arg2 ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1\nCMD2 arg2", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 arg2\nCMD2 arg3\nCMD3\nCMD4 arg4 arg5 arg6", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg5", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg6", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1   arg2  \n CMD2  arg3 \n CMD3 \n  CMD4 arg4 arg5 arg6\nCMD5 ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD3", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg4", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg5", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("arg6", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD5", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), true), } },
            { "CMD \"\"", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD  \"foo bar\"", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD  \"foo bar\\ \" baz", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\\ \"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD \"foo \n bar\" \"\"  baz ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo \n bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("\"\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD \"foo \\\" bar\" baz", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo \\\" bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD  \"foo bar\"  baz", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD \"foo bar\" baz\nCMD2 \"baz\\\\bar\"", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"baz\\\\bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { " CMD  \"foo bar\"  baz \n CMD2  \"baz\\\\bar\"  ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"foo bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("baz", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("\"baz\\\\bar\"", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            // These are some extra edge cases
            { "CMD arg1\n CMD2 arg2", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 \nCMD2 arg2", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 \nCMD2 arg2\n", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "\nCMD arg1 \n\nCMD2 arg2\n", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { " \n \n CMD arg1 \n\nCMD2 arg2\n ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
            { "CMD arg1 \n\n  \n CMD2 arg2\n ", new() { ("CMD", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg1", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), (string.Empty, nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("CMD2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Command), false), ("arg2", nameof(RedisObfuscationUtil.RedisTokenizer.TokenType.Argument), true), } },
        };

        // Test cases from https://github.dev/DataDog/datadog-agent/blob/712c7a7835e0f5aaa47211c4d75a84323eed7fd9/pkg/trace/obfuscate/redis_test.go#L103
        public static TheoryData<string, string> GetRedisObfuscatedQuery() => new()
        {
            { "AUTH my-secret-password", "AUTH ?" },
            { "AUTH james my-secret-password", "AUTH ?" },
            { "AUTH", "AUTH" },
            { "APPEND key value", "APPEND key ?" },
            { "GETSET key value", "GETSET key ?" },
            { "LPUSHX key value", "LPUSHX key ?" },
            { "GEORADIUSBYMEMBER key member radius m|km|ft|mi [WITHCOORD] [WITHDIST] [WITHHASH] [COUNT count] [ASC|DESC] [STORE key] [STOREDIST key]", "GEORADIUSBYMEMBER key ? radius m|km|ft|mi [WITHCOORD] [WITHDIST] [WITHHASH] [COUNT count] [ASC|DESC] [STORE key] [STOREDIST key]" },
            { "RPUSHX key value", "RPUSHX key ?" },
            { "SET key value", "SET key ?" },
            { "SET key value [expiration EX seconds|PX milliseconds] [NX|XX]", "SET key ? [expiration EX seconds|PX milliseconds] [NX|XX]" },
            { "SETNX key value", "SETNX key ?" },
            { "SISMEMBER key member", "SISMEMBER key ?" },
            { "ZRANK key member", "ZRANK key ?" },
            { "ZREVRANK key member", "ZREVRANK key ?" },
            { "ZSCORE key member", "ZSCORE key ?" },
            { "BITFIELD key GET type offset SET type offset value INCRBY type", "BITFIELD key GET type offset SET type offset ? INCRBY type" },
            { "BITFIELD key SET type offset value INCRBY type", "BITFIELD key SET type offset ? INCRBY type" },
            { "BITFIELD key GET type offset INCRBY type", "BITFIELD key GET type offset INCRBY type" },
            { "BITFIELD key SET type offset", "BITFIELD key SET type offset" },
            { "CONFIG SET parameter value", "CONFIG SET parameter ?" },
            { "CONFIG foo bar baz", "CONFIG foo bar baz" },
            { "GEOADD key longitude latitude member longitude latitude member longitude latitude member", "GEOADD key longitude latitude ? longitude latitude ? longitude latitude ?" },
            { "GEOADD key longitude latitude member longitude latitude member", "GEOADD key longitude latitude ? longitude latitude ?" },
            { "GEOADD key longitude latitude member", "GEOADD key longitude latitude ?" },
            { "GEOADD key longitude latitude", "GEOADD key longitude latitude" },
            { "GEOADD key", "GEOADD key" },
            { "GEOHASH key\nGEOPOS key\n GEODIST key", "GEOHASH key\nGEOPOS key\nGEODIST key" },
            { "GEOHASH key member\nGEOPOS key member\nGEODIST key member\n", "GEOHASH key ?\nGEOPOS key ?\nGEODIST key ?" },
            { "GEOHASH key member member member\nGEOPOS key member member \n  GEODIST key member member member", "GEOHASH key ?\nGEOPOS key ?\nGEODIST key ?" },
            { "GEOPOS key member [member ...]", "GEOPOS key ?" },
            { "SREM key member [member ...]", "SREM key ?" },
            { "ZREM key member [member ...]", "ZREM key ?" },
            { "SADD key member [member ...]", "SADD key ?" },
            { "GEODIST key member1 member2 [unit]", "GEODIST key ?" },
            { "LPUSH key value [value ...]", "LPUSH key ?" },
            { "RPUSH key value [value ...]", "RPUSH key ?" },
            { "HSET key field value \nHSETNX key field value\nBLAH", "HSET key field ?\nHSETNX key field ?\nBLAH" },
            { "HSET key field value", "HSET key field ?" },
            { "HSETNX key field value", "HSETNX key field ?" },
            { "LREM key count value", "LREM key count ?" },
            { "LSET key index value", "LSET key index ?" },
            { "SETBIT key offset value", "SETBIT key offset ?" },
            { "SETRANGE key offset value", "SETRANGE key offset ?" },
            { "SETEX key seconds value", "SETEX key seconds ?" },
            { "PSETEX key milliseconds value", "PSETEX key milliseconds ?" },
            { "ZINCRBY key increment member", "ZINCRBY key increment ?" },
            { "SMOVE source destination member", "SMOVE source destination ?" },
            { "RESTORE key ttl serialized-value [REPLACE]", "RESTORE key ttl ? [REPLACE]" },
            { "LINSERT key BEFORE pivot value", "LINSERT key BEFORE pivot ?" },
            { "LINSERT key AFTER pivot value", "LINSERT key AFTER pivot ?" },
            { "HMSET key field value field value", "HMSET key field ? field ?" },
            { "HMSET key field value \n HMSET key field value\n\n ", "HMSET key field ?\nHMSET key field ?" },
            { "HMSET key field", "HMSET key field" },
            { "MSET key value key value", "MSET key ? key ?" },
            { "MSET\nMSET key value", "MSET\nMSET key ?" },
            { "MSET key value", "MSET key ?" },
            { "MSETNX key value key value", "MSETNX key ? key ?" },
            { "ZADD key score member score member", "ZADD key score ? score ?" },
            { "ZADD key NX score member score member", "ZADD key NX score ? score ?" },
            { "ZADD key NX CH score member score member", "ZADD key NX CH score ? score ?" },
            { "ZADD key NX CH INCR score member score member", "ZADD key NX CH INCR score ? score ?" },
            { "ZADD key XX INCR score member score member", "ZADD key XX INCR score ? score ?" },
            { "ZADD key XX INCR score member", "ZADD key XX INCR score ?" },
            { "ZADD key XX INCR score", "ZADD key XX INCR score" },
            {
                @"
CONFIG command
SET k v
			",
                @"CONFIG command
SET k ?"
            },
            // These are some extra ones to catch some edge cases
            { null, null },
            { string.Empty, string.Empty },
            { "\n  \n", "\n  \n" },
            { "EEK key value1 value2", "EEK key value1 value2" },
            { "YIKES key value1 value2", "YIKES key value1 value2" },
            { "OOPSEY key value1 value2", "OOPSEY key value1 value2" },
            { "IMSORRY key value1 value2", "IMSORRY key value1 value2" },
            { "WHOOPSEY key value1 value2", "WHOOPSEY key value1 value2" },
            { "IMSOSORRY key value1 value2", "IMSOSORRY key value1 value2" },
            { "YOUTRYDOINGITTHEN key value1 value2", "YOUTRYDOINGITTHEN key value1 value2" },
            { "ALRIGHTIMOUT key value1 value2", "ALRIGHTIMOUT key value1 value2" },
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
        public void RedisTokenizerTest(string query, SerializableList<(string Token, string TokenType, bool Done)> allExpected)
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

        [Theory]
        [MemberData(nameof(GetRedisObfuscatedQuery))]
        public void RedisObfuscatorTest(string inValue, string expectedValue)
        {
            var actualValue = RedisObfuscationUtil.Obfuscate(inValue);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
