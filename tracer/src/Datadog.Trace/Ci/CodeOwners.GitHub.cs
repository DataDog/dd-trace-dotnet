// <copyright file="CodeOwners.GitHub.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci
{
    internal sealed partial class CodeOwners
    {
        /// <summary>
        /// Reads and validates the owner list from a GitHub rule.
        /// </summary>
        private static class GitHubOwnerTokenizer
        {
            /// <summary>
            /// Splits the owner text, removes duplicates, and reports invalid owners.
            /// </summary>
            public static string[] TokenizeGitHub(string segment, out bool allValid)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    allValid = true;
                    return [];
                }

                var owners = new List<string>();
                var uniqueOwners = new HashSet<string>(StringComparer.Ordinal);
                allValid = true;
                foreach (var token in segment.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (IsValidGitHubOwner(token))
                    {
                        AddUnique(owners, uniqueOwners, token);
                    }
                    else
                    {
                        allValid = false;
                    }
                }

                return owners.Count == 0 ? [] : owners.ToArray();
            }

            /// <summary>
            /// Adds an owner once while keeping the original order.
            /// </summary>
            private static void AddUnique(List<string> owners, HashSet<string> uniqueOwners, string owner)
            {
                if (uniqueOwners.Add(owner))
                {
                    owners.Add(owner);
                }
            }

            /// <summary>
            /// Checks whether a token is a valid GitHub user, team, or email address.
            /// </summary>
            private static bool IsValidGitHubOwner(string token)
            {
                // GitHub users and teams use @user or @organization/team.
                if (token.Length > 1 && token[0] == '@' && token[1] != '@')
                {
                    var slash = token.IndexOf('/');
                    return slash < 0
                               ? IsValidGitHubIdentifier(token, 1, token.Length)
                               : token.IndexOf('/', slash + 1) < 0 &&
                                 IsValidGitHubIdentifier(token, 1, slash) &&
                                 IsValidGitHubIdentifier(token, slash + 1, token.Length);
                }

                // Any other owner must be a valid email address.
                var at = token.IndexOf('@');
                if (at is < 1 or > 100 ||
                    token.Length - at - 1 is < 1 or > 255 ||
                    token.IndexOf('@', at + 1) >= 0)
                {
                    return false;
                }

                for (var i = 0; i < at; i++)
                {
                    if (!IsEmailLocalCharacter(token[i]))
                    {
                        return false;
                    }
                }

                for (var i = at + 1; i < token.Length; i++)
                {
                    if (!IsAsciiLetterOrDigit(token[i]) && token[i] is not '.' and not '-' and not '_')
                    {
                        return false;
                    }
                }

                return IsAsciiLetterOrDigit(token[token.Length - 1]) || token[token.Length - 1] == '_';
            }

            /// <summary>
            /// Checks one GitHub user, organization, or team name inside a token.
            /// </summary>
            private static bool IsValidGitHubIdentifier(string value, int start, int end)
            {
                if (start >= end || !IsAsciiLetterOrDigit(value[start]) || !IsAsciiLetterOrDigit(value[end - 1]))
                {
                    return false;
                }

                var previousWasHyphen = false;
                for (var i = start; i < end; i++)
                {
                    var character = value[i];
                    if (!IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
                    {
                        return false;
                    }

                    if (character == '-' && previousWasHyphen)
                    {
                        return false;
                    }

                    previousWasHyphen = character == '-';
                }

                return true;
            }

            /// <summary>
            /// Checks whether a character is allowed before the at sign in an email address.
            /// </summary>
            private static bool IsEmailLocalCharacter(char character)
                => IsAsciiLetterOrDigit(character) || ".!#$%&'*+/=?^_`{|}~-".IndexOf(character) >= 0;

            /// <summary>
            /// Checks whether a character is an ASCII letter or digit.
            /// </summary>
            private static bool IsAsciiLetterOrDigit(char character)
                => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
        }

        /// <summary>
        /// Stores GitHub rules in last-match-first order.
        /// </summary>
        private sealed class GitHubDocument : Document
        {
            private readonly Entry[] _rules;

            /// <summary>
            /// Initializes a new instance of the <see cref="GitHubDocument"/> class from compiled rules.
            /// </summary>
            private GitHubDocument(Entry[] rules)
            {
                _rules = rules;
            }

            public static GitHubDocument Empty { get; } = new([]);

            /// <summary>
            /// Parses valid GitHub rules and counts invalid lines.
            /// </summary>
            public static GitHubDocument Parse(IEnumerable<string> lines, out int parsingDiagnosticsCount)
            {
                parsingDiagnosticsCount = 0;
                var rules = new List<Entry>();

                foreach (var line in lines)
                {
                    var raw = line.Trim();
                    if (raw.Length == 0 || raw[0] == '#')
                    {
                        continue;
                    }

                    var entry = Entry.ParseGitHub(raw);
                    if (entry is null)
                    {
                        parsingDiagnosticsCount++;
                    }
                    else
                    {
                        rules.Add(entry);
                    }
                }

                // GitHub uses the last matching rule, so search the rules from bottom to top.
                rules.Reverse();
                return new GitHubDocument(rules.ToArray());
            }

            /// <summary>
            /// Returns the owners from the last GitHub rule that matches the path.
            /// </summary>
            public override IEnumerable<string> Match(string path)
            {
                foreach (var rule in _rules)
                {
                    if (rule.Match(path))
                    {
                        return rule.Owners;
                    }
                }

                return [];
            }
        }

        private sealed partial class Entry
        {
            /// <summary>
            /// Parses one GitHub rule and compiles its path pattern.
            /// </summary>
            public static Entry? ParseGitHub(string raw)
            {
                if (raw.StartsWith("\\#"))
                {
                    return null;
                }

                var idxHash = FindUnescapedCharacter(raw, '#');
                var effective = idxHash >= 0 ? raw.Substring(0, idxHash).TrimEnd() : raw;
                if (string.IsNullOrWhiteSpace(effective))
                {
                    return null;
                }

                string patternToken;
                string ownersSegment;
                SplitEscapedEntry(effective, out patternToken, out ownersSegment, out _);

                if (patternToken.Length == 0 || IsUnsupportedGitHubPattern(patternToken))
                {
                    return null;
                }

                var owners = GitHubOwnerTokenizer.TokenizeGitHub(ownersSegment, out var allOwnersValid);
                if (!allOwnersValid)
                {
                    return null;
                }

                var glob = GlobPattern.CompileGitHub(patternToken, includeDescendants: IsDirectoryPattern(patternToken));
                if (glob is null)
                {
                    return null;
                }

                return new Entry(glob, patternToken, exclusion: false, owners);
            }

            /// <summary>
            /// Finds a character that is not escaped with a backslash.
            /// </summary>
            private static int FindUnescapedCharacter(string value, char character)
            {
                for (var i = 0; i < value.Length; i++)
                {
                    if (value[i] == '\\' && i + 1 < value.Length)
                    {
                        i++;
                    }
                    else if (value[i] == character)
                    {
                        return i;
                    }
                }

                return -1;
            }

            /// <summary>
            /// Rejects pattern features that GitHub CODEOWNERS does not support.
            /// </summary>
            private static bool IsUnsupportedGitHubPattern(string patternToken)
            {
                if (patternToken.StartsWith("!"))
                {
                    return true;
                }

                var hasOpeningBracket = false;
                for (var i = 0; i < patternToken.Length; i++)
                {
                    if (patternToken[i] == '\\' && i + 1 < patternToken.Length)
                    {
                        i++;
                    }
                    else if (patternToken[i] == '[')
                    {
                        hasOpeningBracket = true;
                    }
                    else if (patternToken[i] == ']' && hasOpeningBracket)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Checks whether the final path segment names a directory without wildcards.
            /// </summary>
            private static bool IsDirectoryPattern(string patternToken)
            {
                var lastSegmentStart = patternToken.LastIndexOf('/');
                var lastSegment = lastSegmentStart >= 0 ? patternToken.Substring(lastSegmentStart + 1) : patternToken;
                if (lastSegment.Length == 0)
                {
                    return false;
                }

                for (var i = 0; i < lastSegment.Length; i++)
                {
                    if (lastSegment[i] == '\\' && i + 1 < lastSegment.Length)
                    {
                        i++;
                    }
                    else if (lastSegment[i] is '*' or '?')
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
