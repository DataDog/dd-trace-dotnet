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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CodeOwners>();
        private readonly Document _document;

        public CodeOwners(string filePath, Platform platform)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

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
                // No callers pass null today, but normalizing here keeps the API safe to use.
                return [];
            }

            var normalizedPath = path.IndexOf('\\') >= 0 ? path.Replace('\\', '/') : path;

            // Rooted patterns are anchored to the repository root, so collapse any leading slashes:
            // "", "/", and "//C:/file" all normalize to a single rooted form.
            normalizedPath = "/" + normalizedPath.TrimStart('/');

            return _document.Match(normalizedPath);
        }

#pragma warning disable SA1201
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

        private readonly struct GlobPathSegment
        {
            private readonly SegmentPattern? _segment;

            private GlobPathSegment(SegmentPattern? segment, bool isGlobStar, bool requiresSegment)
            {
                _segment = segment;
                IsGlobStar = isGlobStar;
                RequiresSegment = requiresSegment;
            }

            public bool IsGlobStar { get; }

            public bool RequiresSegment { get; }

            public static GlobPathSegment GlobStar(bool requiresSegment) => new(null, isGlobStar: true, requiresSegment: requiresSegment);

            public static GlobPathSegment Pattern(SegmentPattern segment) => new(segment, isGlobStar: false, requiresSegment: false);

            public bool Matches(string path, int start, int end) => _segment!.IsMatch(path, start, end);
        }

        private sealed class GlobPattern
        {
            private readonly GlobPathSegment[] _segments;

            private GlobPattern(GlobPathSegment[] segments)
            {
                _segments = segments;
            }

            public static GlobPattern? CompileGitHub(string pattern, bool includeDescendants)
                => Compile(pattern, Platform.GitHub, includeDescendants);

            public static GlobPattern? CompileGitLab(string pattern)
                => Compile(pattern, Platform.GitLab, includeDescendants: false);

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
                        // A terminal /** means contents below the preceding directory and must
                        // consume at least one path segment on GitHub. GitLab delegates matching
                        // to File.fnmatch, where a terminal ** behaves like * within one segment.
                        // Middle globstars may consume none on both platforms.
                        AddGlobStar(segments, requiresSegment: i == lastSegment - 1);
                    }
                    else if (SegmentPattern.TryCompile(rawSegments[i], platform, out var segment))
                    {
                        segments.Add(GlobPathSegment.Pattern(segment));
                    }
                    else
                    {
                        // Invalid shell character classes invalidate only their own entry.
                        return null;
                    }
                }

                if (hasTrailingSlash || includeDescendants)
                {
                    // A trailing slash denotes a directory, so it cannot match a same-named file.
                    // Descendant expansion inferred for GitHub patterns remains optional because
                    // the base pattern itself may denote either a file or a directory.
                    AddGlobStar(segments, requiresSegment: hasTrailingSlash);
                }

                return new GlobPattern(segments.ToArray());
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
                            // Preserve non-separator escapes for SegmentPattern to compile.
                            segment.Append(character);
                            segment.Append(escapedCharacter);
                            i++;
                            hasTrailingSlash = false;
                            continue;
                        }

                        // An escaped slash is still the path separator in gitignore-style globs;
                        // consume the escape before splitting so it cannot leave a trailing '\\'.
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
                    // zero-or-more followed by one-or-more (or vice versa) is one-or-more.
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
            private readonly Platform _platform;

            private SegmentPattern(string pattern, Platform platform)
            {
                _pattern = pattern;
                _platform = platform;
            }

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
                var negated = contentStart < pattern.Length && pattern[contentStart] is '!' or '^';
                var atomStart = negated ? contentStart + 1 : contentStart;
                if (atomStart < pattern.Length && pattern[atomStart] == ']')
                {
                    return CharacterClassParseResult.Invalid;
                }

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
                        return false;
                    }

                    if (patternIndex < _pattern.Length && _pattern[patternIndex] == '*')
                    {
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

        private abstract class Document
        {
            public abstract IEnumerable<string> Match(string path);
        }

        private sealed partial class Entry
        {
            private readonly GlobPattern _glob;

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

            public bool Match(string path) => _glob.IsMatch(path);
        }
    }
}
