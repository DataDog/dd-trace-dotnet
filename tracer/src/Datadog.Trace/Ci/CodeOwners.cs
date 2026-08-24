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

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Parses and matches GitHub and GitLab CODEOWNERS files.
    /// </summary>
    internal sealed partial class CodeOwners
    {
        internal const long GitHubMaximumFileSizeBytes = 3 * 1024 * 1024;

        private static readonly char[] OwnerSeparators = [' ', '\t'];
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CodeOwners>();
        private readonly Document _document;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeOwners"/> class and loads the selected platform rules.
        /// </summary>
        public CodeOwners(string filePath, Platform platform)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // GitHub ignores CODEOWNERS files larger than 3 MB.
            // https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-file-size
            if (platform == Platform.GitHub && new FileInfo(filePath).Length > GitHubMaximumFileSizeBytes)
            {
                _document = GitHubDocument.Empty;
                Log.Warning<long, string>(
                    "GitHub CODEOWNERS file exceeds the {MaximumSize} byte limit and will be ignored: {Path}",
                    GitHubMaximumFileSizeBytes,
                    filePath);
                return;
            }

            int parsingDiagnosticsCount;
            _document = platform switch
            {
                Platform.GitHub => GitHubDocument.Parse(File.ReadLines(filePath), out parsingDiagnosticsCount),
                Platform.GitLab => GitLabDocument.Parse(File.ReadLines(filePath), out parsingDiagnosticsCount),
                _ => throw new ArgumentOutOfRangeException(nameof(platform)),
            };

            ParsingDiagnosticsCount = parsingDiagnosticsCount;
            if (parsingDiagnosticsCount > 0)
            {
                Log.Warning<int, string>(
                    "CODEOWNERS file contains {Count} invalid lines. Invalid rules were ignored or parsed with errors: {Path}",
                    parsingDiagnosticsCount,
                    filePath);
            }
        }

        internal int ParsingDiagnosticsCount { get; }

        /// <summary>
        /// Tries to load a CODEOWNERS file and handles file access errors.
        /// </summary>
        internal static bool TryLoad(string filePath, Platform platform, [NotNullWhen(true)] out CodeOwners? codeOwners)
        {
            try
            {
                codeOwners = new CodeOwners(filePath, platform);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                Log.Warning(ex, "Unable to load CODEOWNERS file; ownership matching will be skipped: {Path}", filePath);
                codeOwners = null;
                return false;
            }
        }

        /// <summary>
        /// Returns the complete, de‑duplicated owner set that applies to <paramref name="path"/>.
        /// Callers can post‑process the set depending on platform‑specific approval rules.
        /// </summary>
        public IEnumerable<string> Match(string path)
        {
            if (path is null)
            {
                // Returning no owners keeps this method safe if a caller passes null.
                return [];
            }

            var normalizedPath = path.IndexOf('\\') >= 0 ? path.Replace('\\', '/') : path;

            // Rooted patterns are anchored to the repository root, so collapse any leading slashes:
            // "", "/", and "//C:/file" all normalize to a single rooted form.
            normalizedPath = "/" + normalizedPath.TrimStart('/');

            return _document.Match(normalizedPath);
        }

        /// <summary>
        /// Adds an owner once while keeping the original order.
        /// </summary>
        private static void AddUniqueOwner(List<string> owners, HashSet<string> uniqueOwners, string owner)
        {
            if (uniqueOwners.Add(owner))
            {
                owners.Add(owner);
            }
        }

        /// <summary>
        /// Checks whether a character is an ASCII letter or digit.
        /// </summary>
        private static bool IsAsciiLetterOrDigit(char character)
            => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

#pragma warning disable SA1201
        /// <summary>
        /// Identifies the CODEOWNERS syntax to use.
        /// </summary>
        public enum Platform
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

        /// <summary>
        /// Represents either one path segment pattern or a globstar that can consume directories.
        /// </summary>
        private readonly struct GlobPathSegment
        {
            private readonly SegmentPattern? _segment;

            /// <summary>
            /// Initializes a new instance of the <see cref="GlobPathSegment"/> struct.
            /// </summary>
            private GlobPathSegment(SegmentPattern? segment, bool isGlobStar, bool requiresSegment)
            {
                _segment = segment;
                IsGlobStar = isGlobStar;
                RequiresSegment = requiresSegment;
            }

            public bool IsGlobStar { get; }

            public bool RequiresSegment { get; }

            /// <summary>
            /// Creates a globstar that consumes zero or more path segments, or at least one when required.
            /// </summary>
            public static GlobPathSegment GlobStar(bool requiresSegment) => new(null, isGlobStar: true, requiresSegment: requiresSegment);

            /// <summary>
            /// Wraps a normal compiled segment pattern.
            /// </summary>
            public static GlobPathSegment Pattern(SegmentPattern segment) => new(segment, isGlobStar: false, requiresSegment: false);

            /// <summary>
            /// Checks whether this normal segment pattern matches the selected part of a path.
            /// </summary>
            public bool Matches(string path, int start, int end) => _segment!.IsMatch(path, start, end);
        }

        /// <summary>
        /// Matches a full repository path by combining normal segment patterns and globstars.
        /// </summary>
        private sealed class GlobPattern
        {
            private readonly GlobPathSegment[] _segments;

            /// <summary>
            /// Initializes a new instance of the <see cref="GlobPattern"/> class from compiled segments.
            /// </summary>
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
                => Compile(pattern, Platform.GitHub, includeDescendants);

            /// <summary>
            /// Compiles a pattern with GitLab rules.
            /// </summary>
            /// <remarks>
            /// See <see href="https://docs.gitlab.com/user/project/codeowners/reference/#path-matching">GitLab CODEOWNERS path matching</see>.
            /// </remarks>
            public static GlobPattern? CompileGitLab(string pattern)
                => Compile(pattern, Platform.GitLab, includeDescendants: false);

            /// <summary>
            /// Splits a pattern into path segments and compiles each segment with the selected platform rules.
            /// </summary>
            private static GlobPattern? Compile(string pattern, Platform platform, bool includeDescendants)
            {
                var rawSegments = SplitPattern(pattern, out var firstSeparator, out var hasTrailingSlash);
                var rooted = (rawSegments.Length > 0 && rawSegments[0].Length == 0) ||
                             (platform == Platform.GitHub && firstSeparator >= 0 && firstSeparator < pattern.Length - 1);
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
                    if (rawSegments[i] == "**" &&
                        !(platform == Platform.GitLab && i == lastSegment - 1))
                    {
                        // On GitHub, a final /** must match at least one child segment.
                        // On GitLab, a final ** works like * inside the last segment.
                        // A middle ** can match zero or more segments on both platforms.
                        AddGlobStar(segments, requiresSegment: i == lastSegment - 1);
                    }
                    else if (SegmentPattern.TryCompile(rawSegments[i], platform, out var segment))
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

            /// <summary>
            /// Splits a pattern on path separators while keeping escaped characters inside their segment.
            /// </summary>
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

            /// <summary>
            /// Adds a globstar and merges it with the previous one when they are next to each other.
            /// </summary>
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

            /// <summary>
            /// Finds the end of the current path segment.
            /// </summary>
            private static int GetSegmentEnd(string path, int segmentStart)
            {
                var separator = path.IndexOf('/', segmentStart);
                return separator >= 0 ? separator : path.Length;
            }

            /// <summary>
            /// Returns the start of the next path segment, or -1 when there is no next segment.
            /// </summary>
            private static int GetNextSegmentStart(string path, int segmentEnd)
                => segmentEnd < path.Length - 1 ? segmentEnd + 1 : -1;
        }

        /// <summary>
        /// Matches one path segment using literals, escapes, wildcards, and GitLab character classes.
        /// </summary>
        private sealed class SegmentPattern
        {
            private const int MaximumPatternLength = 1_024;
            private const int MaximumMatchSteps = 65_536;

            private readonly string _pattern;
            private readonly Platform _platform;

            /// <summary>
            /// Initializes a new instance of the <see cref="SegmentPattern"/> class.
            /// </summary>
            private SegmentPattern(string pattern, Platform platform)
            {
                _pattern = pattern;
                _platform = platform;
            }

            /// <summary>
            /// Validates one segment and compiles it when its syntax and size are safe.
            /// </summary>
            public static bool TryCompile(string pattern, Platform platform, [NotNullWhen(true)] out SegmentPattern? segment)
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
                    else if (platform == Platform.GitLab && character == '[')
                    {
                        var result = TryParseCharacterClass(pattern, i, default, evaluate: false, out var closingBracket, out _);
                        if (result == CharacterClassParseResult.Invalid)
                        {
                            segment = null;
                            return false;
                        }

                        if (result == CharacterClassParseResult.Success)
                        {
                            i = closingBracket;
                        }
                    }
                }

                segment = new SegmentPattern(pattern, platform);
                return true;
            }

            /// <summary>
            /// Parses a GitLab character class and optionally checks whether it contains a character.
            /// </summary>
            private static CharacterClassParseResult TryParseCharacterClass(
                string pattern,
                int openingBracket,
                char value,
                bool evaluate,
                out int closingBracket,
                out bool matches)
            {
                closingBracket = -1;
                matches = false;
                var contentStart = openingBracket + 1;
                // GitLab allows ! or ^ after [ to negate the class.
                var negated = contentStart < pattern.Length && pattern[contentStart] is '!' or '^';
                var atomStart = negated ? contentStart + 1 : contentStart;
                if (atomStart < pattern.Length && pattern[atomStart] == ']')
                {
                    return CharacterClassParseResult.Invalid;
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
                    return CharacterClassParseResult.NotAClass;
                }

                if (atomStart == closingBracket)
                {
                    return CharacterClassParseResult.Invalid;
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
                            return CharacterClassParseResult.Invalid;
                        }

                        if (evaluate && value >= rangeStart && value <= rangeEnd)
                        {
                            matches = true;
                        }

                        atomIndex = lookahead;
                    }
                    else if (evaluate && value == rangeStart)
                    {
                        matches = true;
                    }
                }

                matches = negated ? !matches : matches;
                return CharacterClassParseResult.Success;
            }

            /// <summary>
            /// Reads one literal or escaped character from a character class.
            /// </summary>
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

            /// <summary>
            /// Matches one pattern token against one path character.
            /// </summary>
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

                if (_platform == Platform.GitLab && token == '[')
                {
                    var result = TryParseCharacterClass(_pattern, patternIndex, value, evaluate: true, out var closingBracket, out var matches);
                    if (result == CharacterClassParseResult.Success)
                    {
                        nextPatternIndex = closingBracket + 1;
                        return matches;
                    }
                }

                nextPatternIndex = patternIndex + 1;
                return token == '?' || token == value;
            }
        }

        /// <summary>
        /// Defines how one platform evaluates its parsed rules.
        /// </summary>
        private abstract class Document
        {
            /// <summary>
            /// Returns the owners that apply to a normalized repository path.
            /// </summary>
            public abstract IEnumerable<string> Match(string path);
        }

        /// <summary>
        /// Stores one compiled CODEOWNERS rule.
        /// </summary>
        private sealed partial class Entry
        {
            private readonly GlobPattern _glob;

            /// <summary>
            /// Initializes a new instance of the <see cref="Entry"/> class with a compiled pattern and owners.
            /// </summary>
            private Entry(GlobPattern glob, string patternKey, bool exclusion, string[] owners)
            {
                _glob = glob;
                PatternKey = patternKey;
                IsExclusion = exclusion;
                Owners = owners;
            }

            public bool IsExclusion { get; }

            public string[] Owners { get; }

            public string PatternKey { get; }

            /// <summary>
            /// Splits a rule at its first unescaped whitespace into the pattern and owner text.
            /// </summary>
            private static void SplitEscapedEntry(string entry, out string pattern, out string owners, out bool hasExplicitOwners)
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
