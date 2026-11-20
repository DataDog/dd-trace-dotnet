// <copyright file="CodeOwners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.CodeOwnership
{
    /// <summary>
    /// Parses and matches GitHub and GitLab CODEOWNERS files.
    /// </summary>
    internal sealed partial class CodeOwners
    {
        internal const long GitHubMaximumFileSizeBytes = 3 * 1024 * 1024;

        private static readonly char[] OwnerSeparators = [' ', '\t'];
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CodeOwners>();
        private readonly RuleSet _rules;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeOwners"/> class and loads the selected dialect rules.
        /// </summary>
        [TestingAndPrivateOnly]
        public CodeOwners(string filePath, Dialect dialect)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // GitHub ignores CODEOWNERS files larger than 3 MB.
            // https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-file-size
            if (dialect == Dialect.GitHub && new FileInfo(filePath).Length > GitHubMaximumFileSizeBytes)
            {
                _rules = GitHubRuleSet.Empty;
                Log.Warning<long, string>(
                    "GitHub CODEOWNERS file exceeds the {MaximumSize} byte limit and will be ignored: {Path}",
                    GitHubMaximumFileSizeBytes,
                    filePath);
                return;
            }

            _rules = ParseRules(File.ReadLines(filePath), dialect, out var parsingDiagnosticsCount);

            ParsingDiagnosticsCount = parsingDiagnosticsCount;
            if (parsingDiagnosticsCount > 0)
            {
                Log.Warning<int, string>(
                    "CODEOWNERS file contains {Count} invalid lines. Invalid rules were ignored or parsed with errors: {Path}",
                    parsingDiagnosticsCount,
                    filePath);
            }
        }

        private CodeOwners(RuleSet rules, int parsingDiagnosticsCount)
        {
            _rules = rules;
            ParsingDiagnosticsCount = parsingDiagnosticsCount;
        }

        [TestingAndPrivateOnly]
        internal int ParsingDiagnosticsCount { get; }

        [TestingAndPrivateOnly]
        internal static CodeOwners Parse(IEnumerable<string> lines, Dialect dialect)
        {
            var rules = ParseRules(lines, dialect, out var parsingDiagnosticsCount);
            return new CodeOwners(rules, parsingDiagnosticsCount);
        }

        /// <summary>
        /// Tries to load a CODEOWNERS file and handles file access errors.
        /// </summary>
        internal static bool TryLoad(string filePath, Dialect dialect, [NotNullWhen(true)] out CodeOwners? codeOwners)
        {
            try
            {
                codeOwners = new CodeOwners(filePath, dialect);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                Log.Warning(ex, "Unable to load CODEOWNERS file; ownership matching will be skipped: {Path}", filePath);
                codeOwners = null;
                return false;
            }
        }

        private static RuleSet ParseRules(IEnumerable<string> lines, Dialect dialect, out int parsingDiagnosticsCount)
            => dialect switch
            {
                Dialect.GitHub => GitHubRuleSet.Parse(lines, out parsingDiagnosticsCount),
                Dialect.GitLab => GitLabRuleSet.Parse(lines, out parsingDiagnosticsCount),
                _ => throw new ArgumentOutOfRangeException(nameof(dialect)),
            };

        /// <summary>
        /// Returns the owners that apply to <paramref name="path"/> using the selected provider's precedence rules.
        /// </summary>
        public string[] Match(string path)
        {
            if (path is null)
            {
                return [];
            }

            var normalizedPath = path.IndexOf('\\') >= 0 ? path.Replace('\\', '/') : path;

            // Rooted patterns are anchored to the repository root. Keep an already-normalized path
            // unchanged, and collapse multiple leading slashes only when needed.
            var leadingSeparatorCount = 0;
            while (leadingSeparatorCount < normalizedPath.Length && normalizedPath[leadingSeparatorCount] == '/')
            {
                leadingSeparatorCount++;
            }

            if (leadingSeparatorCount == 0)
            {
                normalizedPath = "/" + normalizedPath;
            }
            else if (leadingSeparatorCount > 1)
            {
                // Start one character before the content to keep exactly one leading slash.
                normalizedPath = normalizedPath.Substring(leadingSeparatorCount - 1);
            }

            return _rules.Match(normalizedPath);
        }

        private static void AddUniqueOwner(List<string> owners, HashSet<string> uniqueOwners, string owner)
        {
            if (uniqueOwners.Add(owner))
            {
                owners.Add(owner);
            }
        }

#pragma warning disable SA1201
        /// <summary>
        /// Identifies the CODEOWNERS syntax to use.
        /// </summary>
        public enum Dialect
#pragma warning restore SA1201
        {
            GitHub,
            GitLab
        }

        private enum CharacterClassParseResult
        {
            NotAClass,
            Success,
            Invalid
        }

        private readonly struct GlobPathSegment
        {
            private readonly SegmentPattern? _segment;
            private readonly bool _requiresSegment;

            private GlobPathSegment(SegmentPattern? segment, bool requiresSegment)
            {
                _segment = segment;
                _requiresSegment = requiresSegment;
            }

            public bool IsGlobStar => _segment is null;

            public bool RequiresSegment => _requiresSegment;

            /// <summary>
            /// Creates a globstar that consumes zero or more path segments, or at least one when required.
            /// </summary>
            public static GlobPathSegment GlobStar(bool requiresSegment)
                => new(null, requiresSegment);

            /// <summary>
            /// Wraps a normal compiled segment pattern.
            /// </summary>
            public static GlobPathSegment Pattern(SegmentPattern segment) => new(segment, requiresSegment: false);

            /// <summary>
            /// Checks whether this normal segment pattern matches the selected part of a path.
            /// </summary>
            public bool Matches(string path, int start, int end)
                => _segment is not null && _segment.IsMatch(path, start, end);
        }

        private sealed class GlobPattern
        {
            private readonly GlobPathSegment[] _segments;

            private GlobPattern(GlobPathSegment[] segments)
            {
                _segments = segments;
            }

            /// <summary>
            /// Compiles a pattern with GitHub rules.
            /// </summary>
            /// <remarks>
            /// See <see href="https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax">GitHub CODEOWNERS syntax</see>.
            /// </remarks>
            public static GlobPattern? CompileGitHub(string pattern, bool includeDescendants)
                => Compile(pattern, Dialect.GitHub, includeDescendants);

            /// <summary>
            /// Compiles a pattern with GitLab rules.
            /// </summary>
            /// <remarks>
            /// See <see href="https://docs.gitlab.com/user/project/codeowners/reference/#path-matching">GitLab CODEOWNERS path matching</see>.
            /// </remarks>
            public static GlobPattern? CompileGitLab(string pattern)
                => Compile(pattern, Dialect.GitLab, includeDescendants: false);

            private static GlobPattern? Compile(string pattern, Dialect dialect, bool includeDescendants)
            {
                var rawSegments = SplitPattern(pattern, out var firstSeparator, out var hasTrailingSlash);
                var rooted = IsRootedPattern(pattern, rawSegments, firstSeparator, dialect);
                var firstSegment = rooted && rawSegments.Length > 0 && rawSegments[0].Length == 0 ? 1 : 0;
                var lastSegment = rawSegments.Length;
                if (hasTrailingSlash && lastSegment > firstSegment && rawSegments[lastSegment - 1].Length == 0)
                {
                    lastSegment--;
                }

                var segments = new List<GlobPathSegment>(rawSegments.Length + 2);
                if (!rooted)
                {
                    AddGlobStar(segments, requiresSegment: false);
                }

                for (var i = firstSegment; i < lastSegment; i++)
                {
                    // A trailing slash adds a separate descendant globstar, so the preceding ** is not terminal.
                    var isTerminalSegment = i == lastSegment - 1 && !hasTrailingSlash;
                    if (rawSegments[i] == "**" &&
                        !(dialect == Dialect.GitLab && isTerminalSegment))
                    {
                        // On GitHub, a final /** must match at least one child segment.
                        // On GitLab, a final ** works like * inside the last segment.
                        // A middle ** can match zero or more segments on both platforms.
                        AddGlobStar(segments, requiresSegment: isTerminalSegment);
                    }
                    else if (SegmentPattern.TryCompile(rawSegments[i], dialect, out var segment))
                    {
                        segments.Add(GlobPathSegment.Pattern(segment));
                    }
                    else
                    {
                        // Ignore the full rule when one segment is invalid.
                        return null;
                    }
                }

                if (hasTrailingSlash || includeDescendants)
                {
                    // A trailing slash means a directory and must match a child path.
                    // A plain GitHub directory name may also be a file, so its child match is optional.
                    AddGlobStar(segments, requiresSegment: hasTrailingSlash);
                }

                return new GlobPattern(segments.ToArray());
            }

            private static bool IsRootedPattern(string pattern, string[] segments, int firstSeparator, Dialect dialect)
            {
                if (segments.Length > 0 && segments[0].Length == 0)
                {
                    return true;
                }

                // GitHub anchors any pattern that contains a non-terminal slash. GitLab only
                // anchors patterns with a leading slash.
                return dialect == Dialect.GitHub && firstSeparator >= 0 && firstSeparator < pattern.Length - 1;
            }

            private static string[] SplitPattern(string pattern, out int firstSeparator, out bool hasTrailingSlash)
            {
                var segments = new List<string>();
                var segment = new StringBuilder(pattern.Length);
                firstSeparator = -1;
                hasTrailingSlash = false;

                for (var i = 0; i < pattern.Length; i++)
                {
                    var character = pattern[i];
                    if (character == '\\' && i + 1 < pattern.Length)
                    {
                        var escapedCharacter = pattern[i + 1];
                        if (escapedCharacter != '/')
                        {
                            // Keep this escape so SegmentPattern can process it.
                            segment.Append(character);
                            segment.Append(escapedCharacter);
                            i++;
                            hasTrailingSlash = false;
                            continue;
                        }

                        // An escaped slash is still a path separator.
                        i++;
                    }
                    else if (character != '/')
                    {
                        segment.Append(character);
                        hasTrailingSlash = false;
                        continue;
                    }

                    firstSeparator = firstSeparator < 0 ? i : firstSeparator;
                    segments.Add(segment.ToString());
                    segment.Clear();
                    hasTrailingSlash = i == pattern.Length - 1;
                }

                segments.Add(segment.ToString());
                return segments.ToArray();
            }

            /// <summary>
            /// Matches path segments from left to right and lets the latest globstar consume more segments when needed.
            /// </summary>
            public bool IsMatch(string path)
            {
                var patternIndex = 0;
                var pathSegmentStart = path.Length > 1 ? 1 : -1;
                var globStarIndex = -1;
                var globStarPathStart = -1;

                while (pathSegmentStart >= 0)
                {
                    if (patternIndex < _segments.Length && _segments[patternIndex].IsGlobStar)
                    {
                        var globStar = _segments[patternIndex];
                        globStarIndex = patternIndex++;
                        if (globStar.RequiresSegment)
                        {
                            // Consume the required first segment immediately. If the remainder
                            // fails, the fallback below grows the same globstar one segment at a time.
                            var requiredSegmentEnd = GetSegmentEnd(path, pathSegmentStart);
                            pathSegmentStart = GetNextSegmentStart(path, requiredSegmentEnd);
                            globStarPathStart = pathSegmentStart;
                        }
                        else
                        {
                            // First try the zero-directory interpretation.
                            globStarPathStart = pathSegmentStart;
                        }

                        continue;
                    }

                    var pathSegmentEnd = GetSegmentEnd(path, pathSegmentStart);
                    if (patternIndex < _segments.Length &&
                        !_segments[patternIndex].IsGlobStar &&
                        _segments[patternIndex].Matches(path, pathSegmentStart, pathSegmentEnd))
                    {
                        patternIndex++;
                        pathSegmentStart = GetNextSegmentStart(path, pathSegmentEnd);
                        continue;
                    }

                    if (globStarIndex < 0 || globStarPathStart < 0)
                    {
                        return false;
                    }

                    var globStarSegmentEnd = GetSegmentEnd(path, globStarPathStart);
                    globStarPathStart = GetNextSegmentStart(path, globStarSegmentEnd);
                    pathSegmentStart = globStarPathStart;
                    patternIndex = globStarIndex + 1;
                }

                while (patternIndex < _segments.Length &&
                       _segments[patternIndex].IsGlobStar &&
                       !_segments[patternIndex].RequiresSegment)
                {
                    patternIndex++;
                }

                return patternIndex == _segments.Length;
            }

            private static void AddGlobStar(List<GlobPathSegment> segments, bool requiresSegment)
            {
                if (segments.Count > 0 && segments[segments.Count - 1].IsGlobStar)
                {
                    // Adjacent globstars become one. If either needs a segment, the result does too.
                    if (requiresSegment && !segments[segments.Count - 1].RequiresSegment)
                    {
                        segments[segments.Count - 1] = GlobPathSegment.GlobStar(requiresSegment: true);
                    }

                    return;
                }

                segments.Add(GlobPathSegment.GlobStar(requiresSegment));
            }

            private static int GetSegmentEnd(string path, int segmentStart)
            {
                var separator = path.IndexOf('/', segmentStart);
                return separator >= 0 ? separator : path.Length;
            }

            private static int GetNextSegmentStart(string path, int segmentEnd)
                => segmentEnd < path.Length - 1 ? segmentEnd + 1 : -1;
        }

        private sealed class SegmentPattern
        {
            private const int MaximumPatternLength = 1_024;
            private const int MaximumMatchSteps = 65_536;

            private readonly string _pattern;
            private readonly Dialect _dialect;

            private SegmentPattern(string pattern, Dialect dialect)
            {
                _pattern = pattern;
                _dialect = dialect;
            }

            /// <summary>
            /// Validates one segment and compiles it when its syntax and size are safe.
            /// </summary>
            public static bool TryCompile(string pattern, Dialect dialect, [NotNullWhen(true)] out SegmentPattern? segment)
            {
                if (pattern.Length > MaximumPatternLength)
                {
                    segment = null;
                    return false;
                }

                for (var i = 0; i < pattern.Length; i++)
                {
                    var character = pattern[i];
                    if (character == '\\')
                    {
                        if (i + 1 >= pattern.Length)
                        {
                            segment = null;
                            return false;
                        }

                        i++;
                    }
                    else if (dialect == Dialect.GitLab && character == '[')
                    {
                        var characterClass = ParseCharacterClass(pattern, i, value: null);
                        if (characterClass.Result == CharacterClassParseResult.Invalid)
                        {
                            segment = null;
                            return false;
                        }

                        if (characterClass.Result == CharacterClassParseResult.Success)
                        {
                            i = characterClass.ClosingBracket;
                        }
                    }
                }

                segment = new SegmentPattern(pattern, dialect);
                return true;
            }

            private static CharacterClassMatch ParseCharacterClass(string pattern, int openingBracket, char? value)
            {
                var closingBracket = -1;
                var matches = false;
                var contentStart = openingBracket + 1;
                // GitLab allows ! or ^ after [ to negate the class.
                var negated = contentStart < pattern.Length && pattern[contentStart] is '!' or '^';
                var atomStart = negated ? contentStart + 1 : contentStart;
                if (atomStart < pattern.Length && pattern[atomStart] == ']')
                {
                    return new CharacterClassMatch(CharacterClassParseResult.Invalid, -1, matches: false);
                }

                // Find the first closing bracket that is not escaped.
                for (var i = atomStart; i < pattern.Length; i++)
                {
                    if (pattern[i] == '\\' && i + 1 < pattern.Length)
                    {
                        i++;
                    }
                    else if (pattern[i] == ']')
                    {
                        closingBracket = i;
                        break;
                    }
                }

                if (closingBracket < 0)
                {
                    return new CharacterClassMatch(CharacterClassParseResult.NotAClass, -1, matches: false);
                }

                if (atomStart == closingBracket)
                {
                    return new CharacterClassMatch(CharacterClassParseResult.Invalid, -1, matches: false);
                }

                var atomIndex = atomStart;
                // Each item is either one character or a start-end range.
                while (atomIndex < closingBracket)
                {
                    var rangeStart = ReadCharacterClassAtom(pattern, ref atomIndex, closingBracket, out _);
                    var lookahead = atomIndex;
                    var separatorEscaped = false;
                    var separator = lookahead < closingBracket
                                        ? ReadCharacterClassAtom(pattern, ref lookahead, closingBracket, out separatorEscaped)
                                        : default;
                    if (separator == '-' && !separatorEscaped && lookahead < closingBracket)
                    {
                        var rangeEnd = ReadCharacterClassAtom(pattern, ref lookahead, closingBracket, out _);
                        if (rangeStart > rangeEnd)
                        {
                            return new CharacterClassMatch(CharacterClassParseResult.Invalid, -1, matches: false);
                        }

                        if (value is { } character && character >= rangeStart && character <= rangeEnd)
                        {
                            matches = true;
                        }

                        atomIndex = lookahead;
                    }
                    else if (value == rangeStart)
                    {
                        matches = true;
                    }
                }

                matches = value is not null && (negated ? !matches : matches);
                return new CharacterClassMatch(CharacterClassParseResult.Success, closingBracket, matches);
            }

            private static char ReadCharacterClassAtom(
                string pattern,
                ref int index,
                int closingBracket,
                out bool escaped)
            {
                escaped = pattern[index] == '\\' && index + 1 < closingBracket;
                if (escaped)
                {
                    index++;
                }

                return pattern[index++];
            }

            /// <summary>
            /// Matches one path segment and backtracks only to the latest star when a token fails.
            /// </summary>
            public bool IsMatch(string path, int start, int end)
            {
                var patternIndex = 0;
                var pathIndex = start;
                var starPatternIndex = -1;
                var starPathIndex = -1;
                var remainingSteps = MaximumMatchSteps;

                while (pathIndex < end)
                {
                    if (remainingSteps-- == 0)
                    {
                        // Stop malformed patterns from using too much CPU.
                        return false;
                    }

                    if (patternIndex < _pattern.Length && _pattern[patternIndex] == '*')
                    {
                        // First let the star match zero characters.
                        do
                        {
                            patternIndex++;
                        }
                        while (patternIndex < _pattern.Length && _pattern[patternIndex] == '*');

                        starPatternIndex = patternIndex;
                        starPathIndex = pathIndex;
                        continue;
                    }

                    if (TryMatchToken(patternIndex, path[pathIndex], out var nextPatternIndex))
                    {
                        patternIndex = nextPatternIndex;
                        pathIndex++;
                        continue;
                    }

                    if (starPatternIndex < 0)
                    {
                        return false;
                    }

                    // Retry the latest star after letting it consume one more character.
                    patternIndex = starPatternIndex;
                    pathIndex = ++starPathIndex;
                }

                while (patternIndex < _pattern.Length && _pattern[patternIndex] == '*')
                {
                    patternIndex++;
                }

                return patternIndex == _pattern.Length;
            }

            private bool TryMatchToken(int patternIndex, char value, out int nextPatternIndex)
            {
                if (patternIndex >= _pattern.Length)
                {
                    nextPatternIndex = patternIndex;
                    return false;
                }

                var token = _pattern[patternIndex];
                if (token == '\\')
                {
                    nextPatternIndex = patternIndex + 2;
                    return _pattern[patternIndex + 1] == value;
                }

                if (_dialect == Dialect.GitLab && token == '[')
                {
                    var characterClass = ParseCharacterClass(_pattern, patternIndex, value);
                    if (characterClass.Result == CharacterClassParseResult.Success)
                    {
                        nextPatternIndex = characterClass.ClosingBracket + 1;
                        return characterClass.Matches;
                    }
                }

                nextPatternIndex = patternIndex + 1;
                return token == '?' || token == value;
            }

            private readonly struct CharacterClassMatch
            {
                internal CharacterClassMatch(CharacterClassParseResult result, int closingBracket, bool matches)
                {
                    Result = result;
                    ClosingBracket = closingBracket;
                    Matches = matches;
                }

                internal CharacterClassParseResult Result { get; }

                internal int ClosingBracket { get; }

                internal bool Matches { get; }
            }
        }

        private abstract class RuleSet
        {
            /// <summary>
            /// Returns the owners that apply to a normalized repository path.
            /// </summary>
            public abstract string[] Match(string path);
        }

        private sealed partial class Rule
        {
            private readonly GlobPattern _glob;
            private string? _patternKey;

            private Rule(GlobPattern glob, string? patternKey, bool exclusion, string[] owners)
            {
                _glob = glob;
                _patternKey = patternKey;
                IsExclusion = exclusion;
                Owners = owners;
            }

            public bool IsExclusion { get; }

            public string[] Owners { get; }

            /// <summary>
            /// Returns the GitLab pattern key and releases the parser-only reference.
            /// </summary>
            public string GetPatternKeyAndRelease()
            {
                var patternKey = _patternKey;
                _patternKey = null;
                return patternKey ?? throw new InvalidOperationException("Only GitLab rules have a pattern key.");
            }

            private static void SplitRule(string entry, out string pattern, out string owners, out bool hasExplicitOwners)
            {
                var patternEnd = entry.Length;
                for (var i = 0; i < entry.Length; i++)
                {
                    if (entry[i] == '\\' && i + 1 < entry.Length)
                    {
                        i++;
                    }
                    else if (entry[i] is ' ' or '\t')
                    {
                        patternEnd = i;
                        break;
                    }
                }

                pattern = entry.Substring(0, patternEnd);
                owners = patternEnd < entry.Length ? entry.Substring(patternEnd).Trim() : string.Empty;
                hasExplicitOwners = owners.Length > 0;
            }

            /// <summary>
            /// Checks whether this rule matches a normalized repository path.
            /// </summary>
            public bool Match(string path) => _glob.IsMatch(path);
        }
    }
}
