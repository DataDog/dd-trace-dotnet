// <copyright file="RedisObfuscationUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Processors
{
    internal class RedisObfuscationUtil
    {
        private const int MaxRedisNbCommands = 3;

        internal enum ArgumentObfuscationType
        {
            /// <summary>
            /// don't obfuscate
            /// </summary>
            KeepAll,

            /// <summary>
            /// Obfuscate everything after command
            /// </summary>
            /// <remarks>AUTH password</remarks>
            HideAll, // obfuscate all arguments

            /// <summary>
            /// obfuscate 2nd argument
            /// </summary>
            /// <remarks>APPEND key value</remarks>
            /// <remarks>SET key value [expiration EX seconds|PX milliseconds] [NX|XX]</remarks>
            /// <remarks>ZREVRANK key member</remarks>
            HideArg2, // obfuscate 2nd argument

            /// <summary>
            /// obfuscate 3rd argument
            /// </summary>
            /// <remarks>HSET key field value</remarks>
            /// <remarks>RESTORE key ttl serialized-value [REPLACE]</remarks>
            HideArg3, // obfuscate 3rd argument

            /// <summary>
            /// obfuscate 4th argument
            /// </summary>
            /// <remarks>LINSERT key BEFORE|AFTER pivot value</remarks>
            HideArg4,

            /// <summary>
            /// obfuscate all arguments after the first
            /// </summary>
            /// <remarks>GEOHASH key member [member ...]</remarks>
            Keep1,

            /// <summary>
            /// Obfuscate every 2nd argument starting from the command
            /// </summary>
            /// <remarks>MSET key value [key value ...]</remarks>
            HideEvery2,

            /// <summary>
            /// Obfuscate every 2nd argument starting from first
            /// </summary>
            /// <remarks>HMSET key field value [field value ...]</remarks>
            HideEvery2After1,

            /// <summary>
            /// Obfuscate every 3rd argument starting from first
            /// </summary>
            /// <remarks>GEOADD key longitude latitude member [longitude latitude member ...]</remarks>
            HideEvery3After1,

            /// <summary>
            /// If arg 1 is SET, obfuscate argument 3
            /// </summary>
            /// <remarks>CONFIG SET parameter value</remarks>
            HideArg3IfArg1IsSet,

            /// <summary>
            /// Obfuscate every 3rd argument after a SET argument
            /// </summary>
            /// <remarks>BITFIELD key [GET type offset] [SET type offset value] [INCRBY type offset increment] [OVERFLOW WRAP|SAT|FAIL]</remarks>
            Hide3RdArgAfterSet,

            /// <summary>
            /// Obfuscate every 2nd argument after potential optional ones
            /// </summary>
            /// <remarks>ZADD key [NX|XX] [CH] [INCR] score member [score member ...]</remarks>
            ZADD,
        }

        /// <summary>
        /// "Quantizes" (obfuscates) a redis Span's resource name
        /// Based on https://github.dev/DataDog/datadog-agent/blob/712c7a7835e0f5aaa47211c4d75a84323eed7fd9/pkg/trace/obfuscate/redis.go#L28
        /// </summary>
        public static string Quantize(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return string.Empty;
            }

            var truncated = false;
            var commandCount = 0;

            var builder = StringBuilderCache.Acquire(query.Length);
            var startIndex = 0;
            var endIndex = query.Length;

            // skip initial whitespace
            while (startIndex < endIndex && char.IsWhiteSpace(query[startIndex]))
            {
                startIndex++;
            }

            while (startIndex < query.Length && commandCount < MaxRedisNbCommands)
            {
                // Get next command (separated by line break)
                endIndex = query.IndexOf('\n', startIndex: startIndex);
                if (endIndex == -1)
                {
                    // only 1 command, use whole string
                    endIndex = query.Length;
                }

                // skip whitespace
                while (startIndex < endIndex && char.IsWhiteSpace(query[startIndex]))
                {
                    startIndex++;
                }

                if (startIndex >= endIndex)
                {
                    startIndex = endIndex + 1;
                    continue;
                }

                // Get first argument
                var arg1EndIndex = query.IndexOf(' ', startIndex: startIndex, count: (endIndex - startIndex));
                if (arg1EndIndex == -1)
                {
                    // whole command only has one argument
                    arg1EndIndex = endIndex;
                }
                else
                {
                    // remove whitespace from end of arg
                    while (arg1EndIndex > startIndex && char.IsWhiteSpace(query[arg1EndIndex - 1]))
                    {
                        arg1EndIndex--;
                    }
                }

                // Does the argument have the truncation mark '...'
                if (IsTruncated(query, startIndex, arg1EndIndex))
                {
                    truncated = true;
                    startIndex = endIndex + 1;
                    continue;
                }

                var arg2StartIndex = arg1EndIndex;

                // skip whitespace
                while (arg2StartIndex < endIndex && char.IsWhiteSpace(query[arg2StartIndex]))
                {
                    arg2StartIndex++;
                }

                var isCompoundCommand = false;
                var arg2EndIndex = endIndex;

                // Do we have a second argument?
                if (arg2StartIndex < endIndex)
                {
                    // we have more left in the command
                    arg2EndIndex = query.IndexOf(' ', startIndex: arg2StartIndex, count: endIndex - arg2StartIndex);
                    if (arg2EndIndex == -1)
                    {
                        arg2EndIndex = endIndex;
                    }

                    // if yes, is the first argument a compound command?
                    if (IsCompoundCommand(query, startIndex, arg1EndIndex))
                    {
                        isCompoundCommand = true;

                        // remove whitespace from end of arg
                        while (arg2EndIndex > arg2StartIndex && char.IsWhiteSpace(query[arg2EndIndex - 1]))
                        {
                            arg2EndIndex--;
                        }

                        // is the second command truncated?
                        if (IsTruncated(query, arg2StartIndex, arg2EndIndex))
                        {
                            truncated = true;
                            startIndex = endIndex + 1;
                            continue;
                        }
                    }
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                // add the command to the builder
                while (startIndex < arg1EndIndex)
                {
                    builder.Append(char.ToUpperInvariant(query[startIndex]));
                    startIndex++;
                }

                if (isCompoundCommand)
                {
                    builder.Append(' ');
                    while (arg2StartIndex < arg2EndIndex)
                    {
                        builder.Append(char.ToUpperInvariant(query[arg2StartIndex]));
                        arg2StartIndex++;
                    }
                }

                commandCount++;
                truncated = false;
                startIndex = endIndex + 1;
            }

            if (commandCount == MaxRedisNbCommands || truncated)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append('.', 3);
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        /// <summary>
        /// Based on https://github.dev/DataDog/datadog-agent/blob/712c7a7835e0f5aaa47211c4d75a84323eed7fd9/pkg/trace/obfuscate/redis.go#L91
        /// </summary>
        public static string Obfuscate(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return query;
            }

            var done = false;

            var builder = StringBuilderCache.Acquire(query.Length);
            var tokenizer = new RedisTokenizer(query);

            var obfuscationType = ArgumentObfuscationType.HideAll;
            var argCount = 0;
            int? lastSetArg = null;

            while (!done)
            {
                done = tokenizer.Scan(out var token);

                switch (token.TokenType)
                {
                    case RedisTokenizer.TokenType.Command:
                        if (builder.Length > 0)
                        {
                            builder.Append('\n');
                        }

                        builder.Append(query, token.Offset, token.Length);
                        obfuscationType = GetObfuscationType(query, in token);
                        argCount = 0;
                        lastSetArg = null;
                        break;

                    case RedisTokenizer.TokenType.Argument:
                        argCount++;
                        ObfuscateArg(builder, query, obfuscationType, argCount, lastSetArg, in token);

                        // do we need to check for "SET"?
                        if (obfuscationType == ArgumentObfuscationType.Hide3RdArgAfterSet
                         || obfuscationType == ArgumentObfuscationType.HideArg3IfArg1IsSet)
                        {
                            if (IsSetArg(query, in token))
                            {
                                lastSetArg = argCount;
                            }
                        }
                        else if (obfuscationType == ArgumentObfuscationType.ZADD && argCount > 1)
                        {
                            var couldHaveOptionalArgument =
                                (argCount == 2 && !lastSetArg.HasValue)
                             || (argCount == lastSetArg + 1);

                            if (couldHaveOptionalArgument && IsOptionalZaddArg(query, in token))
                            {
                                lastSetArg = argCount;
                            }
                        }

                        break;
                }
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static bool IsSetArg(string query, in RedisTokenizer.Token token)
        {
            return token.Length == 3
                && query[token.Offset] is 's' or 'S'
                && query[token.Offset + 1] is 'e' or 'E'
                && query[token.Offset + 2] is 't' or 'T';
        }

        private static bool IsOptionalZaddArg(string query, in RedisTokenizer.Token token)
        {
            return token.Length switch
            {
                2 => (query[token.Offset] is 'n' or 'N' && query[token.Offset + 1] is 'x' or 'X')
                  || (query[token.Offset] is 'x' or 'X' && query[token.Offset + 1] is 'x' or 'X')
                  || (query[token.Offset] is 'c' or 'C' && query[token.Offset + 1] is 'h' or 'H'),
                4 => query[token.Offset] is 'i' or 'I'
                  && query[token.Offset + 1] is 'n' or 'N'
                  && query[token.Offset + 2] is 'c' or 'C'
                  && query[token.Offset + 3] is 'r' or 'R',
                _ => false
            };
        }

        private static void ObfuscateArg(
            StringBuilder sb,
            string query,
            ArgumentObfuscationType obfuscationType,
            int argNumber,
            int? lastSetArg,
            in RedisTokenizer.Token token)
        {
            switch (obfuscationType)
            {
                // don't add anything extra for these cases
                case ArgumentObfuscationType.HideAll when argNumber > 1:
                case ArgumentObfuscationType.Keep1 when argNumber > 2:
                    return;

                // obfuscate the argument for all of these cases
                case ArgumentObfuscationType.HideAll when argNumber == 1:
                case ArgumentObfuscationType.HideArg2 when argNumber == 2:
                case ArgumentObfuscationType.HideArg3 when argNumber == 3:
                case ArgumentObfuscationType.HideArg4 when argNumber == 4:
                case ArgumentObfuscationType.Keep1 when argNumber > 1:
                case ArgumentObfuscationType.HideEvery2 when argNumber % 2 == 0:
                case ArgumentObfuscationType.HideEvery2After1 when argNumber != 1 && (argNumber - 1) % 2 == 0:
                case ArgumentObfuscationType.HideEvery3After1 when argNumber != 1 && (argNumber - 1) % 3 == 0:
                case ArgumentObfuscationType.HideArg3IfArg1IsSet when lastSetArg == 1 && argNumber == 3:
                case ArgumentObfuscationType.Hide3RdArgAfterSet when lastSetArg.HasValue && (argNumber - lastSetArg.Value) == 3:
                case ArgumentObfuscationType.ZADD when argNumber > 1 && (argNumber - (lastSetArg ?? 1)) % 2 == 0:
                    sb.Append(' ')
                      .Append('?');
                    return;

                // add the real argument for all other cases
                // KeepAll
                // HideArg2 when argCount != 2
                // HideArg3 when argCount != 3
                // HideArg4 when argCount != 4
                // Keep1 when argNumber == 1
                // HideEvery2 when argCount % 2 != 0:
                // HideEvery2After1 when argCount == 1 || (argCount - 1) % 2 != 0:
                // HideEvery3After1 when argCount == 1 || (argCount - 1) % 3 != 0:
                // HideArg3IfArg1IsSet when lastSetArg != 1 || argCount != 3
                // Hide3RdArgAfterSet when !lastSetArg || (argNumber - lastSetArg.Value) % 3 == 0:
                default:
                    sb.Append(' ')
                      .Append(query, token.Offset, token.Length);
                    return;
            }

            // KeepAll, // don't obfuscate
            // HideAll, // obfuscate all arguments
            // HideArg2, // obfuscate 2nd argument
            // HideArg3, // obfuscate 3rd argument
            // HideArg4, // obfuscate 4th argument
            // Keep1, // obfuscate all arguments after the first
            // HideEvery2, // Obfuscate every 2nd argument starting from the command
            // HideEvery3After1, // Obfuscate every 3rd argument starting from first
            // HideEvery2After1, // Obfuscate every 2nd argument starting from first
            // HideArg3IfArg1IsSet, // If arg 1 is SET, obfuscate argument 3
            // Hide3RdArgAfterSet, // Obfuscate every 3rd argument after a SET argument
            // ZADD, // Obfuscate the ZADD command with all its craziness
        }

        private static bool IsTruncated(string query, int startIndex, int endIndex)
        {
            return endIndex > startIndex + 3
                && query[endIndex - 1] == '.'
                && query[endIndex - 2] == '.'
                && query[endIndex - 3] == '.';
        }

        /// <summary>
        /// Is the redis command a compound command (consists of 2 works)?
        /// </summary>
        internal static bool IsCompoundCommand(string query, int startIndex, int endIndex)
        {
            // The following commands are 2-part commands, so check if the full argument matches this
            // "CLIENT", "CLUSTER", "CONFIG", "COMMAND", "DEBUG", "SCRIPT"
            if (endIndex - startIndex is < 5 or > 7)
            {
                return false;
            }

            var firstLetter = char.ToUpperInvariant(query[startIndex]);

            if (firstLetter == 'C')
            {
                if (char.ToUpperInvariant(query[startIndex + 1]) == 'L')
                {
                    return (endIndex == startIndex + 6
                         && char.ToUpperInvariant(query[startIndex + 2]) == 'I'
                         && char.ToUpperInvariant(query[startIndex + 3]) == 'E'
                         && char.ToUpperInvariant(query[startIndex + 4]) == 'N'
                         && char.ToUpperInvariant(query[startIndex + 5]) == 'T')
                        || (endIndex == startIndex + 7
                         && char.ToUpperInvariant(query[startIndex + 2]) == 'U'
                         && char.ToUpperInvariant(query[startIndex + 3]) == 'S'
                         && char.ToUpperInvariant(query[startIndex + 4]) == 'T'
                         && char.ToUpperInvariant(query[startIndex + 5]) == 'E'
                         && char.ToUpperInvariant(query[startIndex + 6]) == 'R');
                }
                else if (char.ToUpperInvariant(query[startIndex + 1]) == 'O')
                {
                    return (endIndex == startIndex + 6
                         && char.ToUpperInvariant(query[startIndex + 2]) == 'N'
                         && char.ToUpperInvariant(query[startIndex + 3]) == 'F'
                         && char.ToUpperInvariant(query[startIndex + 4]) == 'I'
                         && char.ToUpperInvariant(query[startIndex + 5]) == 'G')
                        || (endIndex == startIndex + 7
                         && char.ToUpperInvariant(query[startIndex + 2]) == 'M'
                         && char.ToUpperInvariant(query[startIndex + 3]) == 'M'
                         && char.ToUpperInvariant(query[startIndex + 4]) == 'A'
                         && char.ToUpperInvariant(query[startIndex + 5]) == 'N'
                         && char.ToUpperInvariant(query[startIndex + 6]) == 'D');
                }
            }
            else if (firstLetter == 'D')
            {
                return (endIndex == startIndex + 5)
                    && char.ToUpperInvariant(query[startIndex + 1]) == 'E'
                    && char.ToUpperInvariant(query[startIndex + 2]) == 'B'
                    && char.ToUpperInvariant(query[startIndex + 3]) == 'U'
                    && char.ToUpperInvariant(query[startIndex + 4]) == 'G';
            }
            else if (firstLetter == 'S')
            {
                return (endIndex == startIndex + 6)
                    && char.ToUpperInvariant(query[startIndex + 1]) == 'C'
                    && char.ToUpperInvariant(query[startIndex + 2]) == 'R'
                    && char.ToUpperInvariant(query[startIndex + 3]) == 'I'
                    && char.ToUpperInvariant(query[startIndex + 4]) == 'P'
                    && char.ToUpperInvariant(query[startIndex + 5]) == 'T';
            }

            return false;
        }

        private static ArgumentObfuscationType GetObfuscationType(string query, in RedisTokenizer.Token token)
        {
            // The following code is equivalent but allocation free
            // In C#12 we can do this easily with Span<char> (pattern matching of span against const string)
            // return query.Substring(token.Offset, token.Length).ToUpperInvariant() switch
            // {
            //     "AUTH" => ArgumentObfuscationType.HideAll,
            //     "APPEND" or "GETSET" or "LPUSHX" or "GEORADIUSBYMEMBER" or "RPUSHX" or "SET" or "SETNX" or "SISMEMBER" or "ZRANK" or "ZREVRANK" or "ZSCORE" => ArgumentObfuscationType.HideArg2,
            //     "HSET" or "HSETNX" or "LREM" or "LSET" or "SETBIT" or "SETEX" or "PSETEX" or "SETRANGE" or "ZINCRBY" or "SMOVE" or "RESTORE" => ArgumentObfuscationType.HideArg3,
            //     "LINSERT" => ArgumentObfuscationType.HideArg4,
            //     "GEOHASH" or "GEOPOS" or "GEODIST" or "LPUSH" or "RPUSH" or "SREM" or "ZREM" or "SADD" => ArgumentObfuscationType.Keep1,
            //     "GEOADD" => ArgumentObfuscationType.HideEvery3After1,
            //     "HMSET" => ArgumentObfuscationType.HideEvery2After1,
            //     "MSET" or "MSETNX" => ArgumentObfuscationType.HideEvery2,
            //     "CONFIG" => ArgumentObfuscationType.HideArg3IfArg1IsSet,
            //     "BITFIELD" => ArgumentObfuscationType.Hide3RdArgAfterSet,
            //     "ZADD" => ArgumentObfuscationType.ZADD,
            //     _ => ArgumentObfuscationType.KeepAll,
            // };
            return token.Length switch
            {
                3 => Get3(query, in token),
                4 => Get4(query, in token),
                5 => Get5(query, in token),
                6 => Get6(query, in token),
                7 => Get7(query, in token),
                8 => Get8(query, in token),
                9 => Get9(query, in token),
                17 => Get17(query, in token),
                _ => ArgumentObfuscationType.KeepAll,
            };

            static ArgumentObfuscationType Get3(string query, in RedisTokenizer.Token token)
            {
                if (char.ToUpperInvariant(query[token.Offset]) == 'S'
                 && char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                 && char.ToUpperInvariant(query[token.Offset + 2]) == 'T')
                {
                    return ArgumentObfuscationType.HideArg2;
                }

                return ArgumentObfuscationType.KeepAll;
            }

            static ArgumentObfuscationType Get4(string query, in RedisTokenizer.Token token)
            {
                return char.ToUpperInvariant(query[token.Offset]) switch
                {
                    'A' when char.ToUpperInvariant(query[token.Offset + 1]) == 'U'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'H' => ArgumentObfuscationType.HideAll,
                    'H' when char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'T' => ArgumentObfuscationType.HideArg3,
                    'L' when (char.ToUpperInvariant(query[token.Offset + 1]) == 'R'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'M')
                          || (char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'T') => ArgumentObfuscationType.HideArg3,
                    'S' when (char.ToUpperInvariant(query[token.Offset + 1]) == 'R'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'M')
                          || (char.ToUpperInvariant(query[token.Offset + 1]) == 'A'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'D'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'D') => ArgumentObfuscationType.Keep1,
                    'Z' when (char.ToUpperInvariant(query[token.Offset + 1]) == 'R'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'M') => ArgumentObfuscationType.Keep1,
                    'M' when (char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'T') => ArgumentObfuscationType.HideEvery2,
                    'Z' when (char.ToUpperInvariant(query[token.Offset + 1]) == 'A'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'D'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'D') => ArgumentObfuscationType.ZADD,
                    _ => ArgumentObfuscationType.KeepAll,
                };
            }

            static ArgumentObfuscationType Get5(string query, in RedisTokenizer.Token token)
            {
                return char.ToUpperInvariant(query[token.Offset]) switch
                {
                    'S' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'X' => ArgumentObfuscationType.HideArg2,
                    'Z' when char.ToUpperInvariant(query[token.Offset + 1]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'A'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'K' => ArgumentObfuscationType.HideArg2,
                    'S' when (char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'E'
                           && char.ToUpperInvariant(query[token.Offset + 4]) == 'X')
                          || (char.ToUpperInvariant(query[token.Offset + 1]) == 'M'
                           && char.ToUpperInvariant(query[token.Offset + 2]) == 'O'
                           && char.ToUpperInvariant(query[token.Offset + 3]) == 'V'
                           && char.ToUpperInvariant(query[token.Offset + 4]) == 'E') => ArgumentObfuscationType.HideArg3,
                    'L' or 'R' when char.ToUpperInvariant(query[token.Offset + 1]) == 'P'
                                 && char.ToUpperInvariant(query[token.Offset + 2]) == 'U'
                                 && char.ToUpperInvariant(query[token.Offset + 3]) == 'S'
                                 && char.ToUpperInvariant(query[token.Offset + 4]) == 'H' => ArgumentObfuscationType.Keep1,
                    'H' when char.ToUpperInvariant(query[token.Offset + 1]) == 'M'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'T' => ArgumentObfuscationType.HideEvery2After1,
                    _ => ArgumentObfuscationType.KeepAll,
                };
            }

            static ArgumentObfuscationType Get6(string query, in RedisTokenizer.Token token)
            {
                return char.ToUpperInvariant(query[token.Offset]) switch
                {
                    'A' when char.ToUpperInvariant(query[token.Offset + 1]) == 'P'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'P'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'D' => ArgumentObfuscationType.HideArg2,
                    'G' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'T' => ArgumentObfuscationType.HideArg2,
                    'L' or 'R' when char.ToUpperInvariant(query[token.Offset + 1]) == 'P'
                                 && char.ToUpperInvariant(query[token.Offset + 2]) == 'U'
                                 && char.ToUpperInvariant(query[token.Offset + 3]) == 'S'
                                 && char.ToUpperInvariant(query[token.Offset + 4]) == 'H'
                                 && char.ToUpperInvariant(query[token.Offset + 5]) == 'X' => ArgumentObfuscationType.HideArg2,
                    'Z' when char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'C'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'O'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'E' => ArgumentObfuscationType.HideArg2,
                    'S' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'B'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'I'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'T' => ArgumentObfuscationType.HideArg3,
                    'H' when char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'X' => ArgumentObfuscationType.HideArg3,
                    'P' when char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'X' => ArgumentObfuscationType.HideArg3,
                    'G' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'O'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'P'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'O'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'S' => ArgumentObfuscationType.Keep1,
                    'G' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'O'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'A'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'D'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'D' => ArgumentObfuscationType.HideEvery3After1,
                    'M' when char.ToUpperInvariant(query[token.Offset + 1]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'X' => ArgumentObfuscationType.HideEvery2,
                    'C' when char.ToUpperInvariant(query[token.Offset + 1]) == 'O'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'F'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'I'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'G' => ArgumentObfuscationType.HideArg3IfArg1IsSet,

                    _ => ArgumentObfuscationType.KeepAll,
                };
            }

            static ArgumentObfuscationType Get7(string query, in RedisTokenizer.Token token)
            {
                return char.ToUpperInvariant(query[token.Offset]) switch
                {
                    'Z' when char.ToUpperInvariant(query[token.Offset + 1]) == 'I'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'C'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'B'
                          && char.ToUpperInvariant(query[token.Offset + 6]) == 'Y' => ArgumentObfuscationType.HideArg3,
                    'R' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'O'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 6]) == 'E' => ArgumentObfuscationType.HideArg3,
                    'L' when char.ToUpperInvariant(query[token.Offset + 1]) == 'I'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'S'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 6]) == 'T' => ArgumentObfuscationType.HideArg4,
                    'G' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'O'
                          && ((char.ToUpperInvariant(query[token.Offset + 3]) == 'H'
                           && char.ToUpperInvariant(query[token.Offset + 4]) == 'A'
                           && char.ToUpperInvariant(query[token.Offset + 5]) == 'S'
                           && char.ToUpperInvariant(query[token.Offset + 6]) == 'H')
                          || (char.ToUpperInvariant(query[token.Offset + 3]) == 'D'
                           && char.ToUpperInvariant(query[token.Offset + 4]) == 'I'
                           && char.ToUpperInvariant(query[token.Offset + 5]) == 'S'
                           && char.ToUpperInvariant(query[token.Offset + 6]) == 'T')) => ArgumentObfuscationType.Keep1,
                    _ => ArgumentObfuscationType.KeepAll,
                };
            }

            static ArgumentObfuscationType Get8(string query, in RedisTokenizer.Token token)
            {
                return char.ToUpperInvariant(query[token.Offset]) switch
                {
                    'Z' when char.ToUpperInvariant(query[token.Offset + 1]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'V'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'A'
                          && char.ToUpperInvariant(query[token.Offset + 6]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 7]) == 'K' => ArgumentObfuscationType.HideArg2,
                    'S' when char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'R'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'A'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'N'
                          && char.ToUpperInvariant(query[token.Offset + 6]) == 'G'
                          && char.ToUpperInvariant(query[token.Offset + 7]) == 'E' => ArgumentObfuscationType.HideArg3,
                    'B' when char.ToUpperInvariant(query[token.Offset + 1]) == 'I'
                          && char.ToUpperInvariant(query[token.Offset + 2]) == 'T'
                          && char.ToUpperInvariant(query[token.Offset + 3]) == 'F'
                          && char.ToUpperInvariant(query[token.Offset + 4]) == 'I'
                          && char.ToUpperInvariant(query[token.Offset + 5]) == 'E'
                          && char.ToUpperInvariant(query[token.Offset + 6]) == 'L'
                          && char.ToUpperInvariant(query[token.Offset + 7]) == 'D' => ArgumentObfuscationType.Hide3RdArgAfterSet,
                    _ => ArgumentObfuscationType.KeepAll,
                };
            }

            static ArgumentObfuscationType Get9(string query, in RedisTokenizer.Token token)
            {
                if (char.ToUpperInvariant(query[token.Offset]) == 'S'
                 && char.ToUpperInvariant(query[token.Offset + 1]) == 'I'
                 && char.ToUpperInvariant(query[token.Offset + 2]) == 'S'
                 && char.ToUpperInvariant(query[token.Offset + 3]) == 'M'
                 && char.ToUpperInvariant(query[token.Offset + 4]) == 'E'
                 && char.ToUpperInvariant(query[token.Offset + 5]) == 'M'
                 && char.ToUpperInvariant(query[token.Offset + 6]) == 'B'
                 && char.ToUpperInvariant(query[token.Offset + 7]) == 'E'
                 && char.ToUpperInvariant(query[token.Offset + 8]) == 'R')
                {
                    return ArgumentObfuscationType.HideArg2;
                }

                return ArgumentObfuscationType.KeepAll;
            }

            static ArgumentObfuscationType Get17(string query, in RedisTokenizer.Token token)
            {
                if (char.ToUpperInvariant(query[token.Offset]) == 'G'
                 && char.ToUpperInvariant(query[token.Offset + 1]) == 'E'
                 && char.ToUpperInvariant(query[token.Offset + 2]) == 'O'
                 && char.ToUpperInvariant(query[token.Offset + 3]) == 'R'
                 && char.ToUpperInvariant(query[token.Offset + 4]) == 'A'
                 && char.ToUpperInvariant(query[token.Offset + 5]) == 'D'
                 && char.ToUpperInvariant(query[token.Offset + 6]) == 'I'
                 && char.ToUpperInvariant(query[token.Offset + 7]) == 'U'
                 && char.ToUpperInvariant(query[token.Offset + 8]) == 'S'
                 && char.ToUpperInvariant(query[token.Offset + 9]) == 'B'
                 && char.ToUpperInvariant(query[token.Offset + 10]) == 'Y'
                 && char.ToUpperInvariant(query[token.Offset + 11]) == 'M'
                 && char.ToUpperInvariant(query[token.Offset + 12]) == 'E'
                 && char.ToUpperInvariant(query[token.Offset + 13]) == 'M'
                 && char.ToUpperInvariant(query[token.Offset + 14]) == 'B'
                 && char.ToUpperInvariant(query[token.Offset + 15]) == 'E'
                 && char.ToUpperInvariant(query[token.Offset + 16]) == 'R')
                {
                    return ArgumentObfuscationType.HideArg2;
                }

                return ArgumentObfuscationType.KeepAll;
            }
        }

        public struct RedisTokenizer
        {
            private readonly string _query;
            private int _offset;
            private int _finalOffset;
            private bool _done;
            private TokenType _state = TokenType.Command;

            public RedisTokenizer(string query)
            {
                _query = query;
                _offset = 0;
                _finalOffset = _query.Length - 1;

                // skip final whitespace
                while (_finalOffset > 0 && query[_finalOffset] is ' ' or '\t' or '\r' or '\n')
                {
                    _finalOffset--;
                }

                // skip initial whitespace
                while (_offset < _finalOffset && query[_offset] is ' ' or '\t' or '\r' or '\n')
                {
                    _offset++;
                }

                _done = _offset > _finalOffset;
            }

            public enum TokenType
            {
                Command,
                Argument,
            }

            /// <summary>
            /// Returns the next token. Returns true if we're finished
            /// </summary>
            public bool Scan(out Token token) =>
                _state == TokenType.Command
                    ? ScanCommand(out token)
                    : ScanArgument(out token);

            private bool ScanCommand(out Token token)
            {
                var initialOffset = _offset;
                var started = false;
                while (!_done)
                {
                    switch (_query[_offset])
                    {
                        case '\n':
                            token = new Token(initialOffset, _offset - initialOffset, TokenType.Command);
                            // increment offset past this token
                            Next();
                            return _done;
                        case ' ':
                            if (!started)
                            {
                                // skip spaces preceding token
                                SkipSpace(skipTrailingLineBreak: false);
                                initialOffset = _offset;
                                break;
                            }

                            // done scanning command, next word is an argument
                            _state = TokenType.Argument;
                            token = new Token(initialOffset, _offset - initialOffset, TokenType.Command);
                            // don't include the subsequent white space in the token
                            SkipSpace(skipTrailingLineBreak: true);

                            return _done;

                        default:
                            started = true;
                            Next();
                            break;
                    }
                }

                // We're done, so return final token
                token = new Token(initialOffset, _offset - initialOffset, TokenType.Command);
                return true;
            }

            private bool ScanArgument(out Token token)
            {
                var initialOffset = _offset;

                var quoted = false;
                var escape = false;
                while (!_done)
                {
                    switch (_query[_offset])
                    {
                        case '\\':
                            escape = !escape;
                            Next();
                            break;
                        case '\n':
                            if (!quoted)
                            {
                                // last argument, new command follows
                                _state = TokenType.Command;
                                token = new Token(initialOffset, _offset - initialOffset, TokenType.Argument);
                                // increment offset past this token
                                Next();
                                return _done;
                            }

                            escape = false;
                            Next();
                            break;
                        case '"':
                            if (!escape)
                            {
                                // this quote wasn't escaped, toggle quoted mode
                                quoted = !quoted;
                            }

                            escape = false;
                            Next();
                            break;
                        case ' ':
                            if (!quoted)
                            {
                                token = new Token(initialOffset, _offset - initialOffset, TokenType.Argument);
                                SkipSpace(skipTrailingLineBreak: true);
                                return _done;
                            }

                            escape = false;
                            Next();
                            break;
                        default:
                            escape = false;
                            Next();
                            break;
                    }
                }

                // We're done, so return final token
                token = new Token(initialOffset, _offset - initialOffset, TokenType.Argument);
                return true;
            }

            private void SkipSpace(bool skipTrailingLineBreak)
            {
                while (!_done && (_query[_offset] == ' ' || _query[_offset] == '\t' || _query[_offset] == '\r'))
                {
                    Next();
                }

                if (!_done && _query[_offset] == '\n')
                {
                    // next token is a command
                    _state = TokenType.Command;

                    // if we have advanced to a line break, skip over it
                    if (skipTrailingLineBreak)
                    {
                        Next();
                    }
                }
            }

            private void Next()
            {
                _offset++;
                if (_offset > _finalOffset)
                {
                    _done = true;
                }
            }

            public readonly struct Token
            {
                public readonly int Offset;
                public readonly int Length;
                public readonly TokenType TokenType;

                public Token(int offset, int length, TokenType tokenType)
                {
                    Offset = offset;
                    Length = length;
                    TokenType = tokenType;
                }
            }
        }
    }
}
