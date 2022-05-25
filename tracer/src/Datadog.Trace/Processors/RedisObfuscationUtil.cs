// <copyright file="RedisObfuscationUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Processors
{
    internal class RedisObfuscationUtil
    {
        private const int MaxRedisNbCommands = 3;

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
            else if (query[startIndex] is 's' or 'S')
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

        public class RedisTokenizer
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
