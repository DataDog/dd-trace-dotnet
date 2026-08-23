// <copyright file="CodeOwners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// A CODEOWNERS parser that follows the GitHub and GitLab specifications: last matching rule wins,
    /// rooted and unrooted (globstar-relative) paths, directory and wildcard patterns, globstars (**),
    /// inline comments (GitHub), sections with default owners, optional sections, approval counts,
    /// role owners (@@role) and exclusion patterns (GitLab). Matching is case-sensitive.
    /// Usage:
    ///   var owners = new CodeOwners(pathToFile, CodeOwners.Platform.GitLab).Match("src/app/Program.cs");
    ///   // owners is an IEnumerable{string} of unique owners that apply to that file.
    /// </summary>
    internal sealed class CodeOwners
    {
        internal const long GitHubMaximumFileSizeBytes = 3 * 1024 * 1024;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CodeOwners>();
        private static readonly Regex SectionHeaderRegex = new(
            @"^\s*(\^)?\[(?<name>.*?)\](?:\[(?<cnt>[\s\d]*)\])?(?<defaults>\s*[@\w.\-/\s]*)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex GitLabRoleReferenceRegex = new(
            @"(?<![\w@])@@(?:developer|maintainer|owner)s?(?=\s|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly List<Section> _sections;
        private readonly Platform _platform;

        public CodeOwners(string filePath, Platform platform)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            _platform = platform;
            if (platform == Platform.GitHub && new FileInfo(filePath).Length > GitHubMaximumFileSizeBytes)
            {
                _sections = [];
                Log.Warning<long, string>(
                    "GitHub CODEOWNERS file exceeds the {MaximumSize} byte limit and will be ignored: {Path}",
                    GitHubMaximumFileSizeBytes,
                    filePath);
                return;
            }

            _sections = Parse(File.ReadLines(filePath), platform, out var parsingDiagnosticsCount);
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

            if (_platform == Platform.GitHub)
            {
                // GitHub has no sections: the whole file is a single ordered rule set where
                // the last matching pattern takes precedence over all previous ones.
                for (var i = _sections.Count - 1; i >= 0; i--)
                {
                    if (_sections[i].TryMatchGitHub(normalizedPath, out var sectionOwners))
                    {
                        return sectionOwners;
                    }
                }

                return [];
            }

            // GitLab evaluates each section independently and combines their owners.
            // The set is allocated lazily because most paths match at most one section.
            HashSet<string>? owners = null;
            foreach (var section in _sections)
            {
                if (section.TryMatchGitLab(normalizedPath, out var sectionOwners))
                {
                    owners ??= new HashSet<string>(StringComparer.Ordinal);
                    foreach (var o in sectionOwners)
                    {
                        owners.Add(o);
                    }
                }
            }

            return owners ?? [];
        }

        private static List<Section> Parse(IEnumerable<string> lines, Platform platform, out int parsingDiagnosticsCount)
        {
            parsingDiagnosticsCount = 0;
            var sections = new List<Section>();
            var current = Section.CreateUnnamed();
            var currentDefaultOwners = current.DefaultOwners;
            Dictionary<string, Section>? namedSections = platform == Platform.GitLab
                                                              ? new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase)
                                                              : null;
            sections.Add(current);

            foreach (var line in lines)
            {
                var raw = line.Trim();
                if (raw.Length == 0)
                {
                    continue;
                }

                if (TryParseSectionHeader(raw, platform, out var newSection, out var sectionHasDiagnostics))
                {
                    if (sectionHasDiagnostics)
                    {
                        parsingDiagnosticsCount++;
                    }

                    currentDefaultOwners = newSection.DefaultOwners;
                    if (namedSections is not null && namedSections.TryGetValue(newSection.Name, out var existingSection))
                    {
                        existingSection.MergeMetadata(newSection);
                        current = existingSection;
                    }
                    else
                    {
                        current = newSection;
                        sections.Add(current);
                        namedSections?.Add(current.Name, current);
                    }

                    continue;
                }

                if (platform == Platform.GitLab && IsUnparsableSectionHeader(raw))
                {
                    // GitLab reports malformed header-like lines and skips them rather than
                    // reinterpreting them as path patterns.
                    parsingDiagnosticsCount++;
                    continue;
                }

                if (raw[0] == '#')
                {
                    // Comment line. GitLab parses owners found inside comments so they appear in MR widget,
                    // but those owners are not bound to any path pattern, so we ignore them for matching.
                    continue;
                }

                var entry = Entry.Parse(raw, platform, currentDefaultOwners, out var entryHasDiagnostics);
                if (entry is not null)
                {
                    if (entryHasDiagnostics)
                    {
                        parsingDiagnosticsCount++;
                    }

                    current.Add(entry, replaceDuplicatePattern: platform == Platform.GitLab);
                }
                else
                {
                    parsingDiagnosticsCount++;
                }
            }

            // Reverse the entries of every section so the last rule in the file is evaluated first
            // at match time, without additional copies.
            foreach (var s in sections)
            {
                s.Seal();
            }

            return sections;
        }

        private static bool TryParseSectionHeader(
            string raw,
            Platform platform,
            [NotNullWhen(true)] out Section? section,
            out bool hasDiagnostics)
        {
            // Accepted forms:
            //   [Docs]
            //   ^[Go]
            //   [Backend][2] @team @another
            var m = SectionHeaderRegex.Match(raw);
            if (!m.Success)
            {
                section = null;
                hasDiagnostics = false;
                return false;
            }

            var required = !m.Groups[1].Success; // ^ prefix => optional section
            var name = m.Groups["name"].Value.Trim();
            hasDiagnostics = platform == Platform.GitHub ||
                             name.Length == 0 ||
                             !IsStrictSectionHeader(raw);

            var approvals = 0;
            if (m.Groups["cnt"].Success)
            {
                if (int.TryParse(m.Groups["cnt"].Value, out var val))
                {
                    approvals = val;
                }
                else
                {
                    hasDiagnostics = true;
                }
            }

            hasDiagnostics |= !required && approvals > 0;

            // Only parse the owner span recognized by GitLab's permissive header grammar. Text
            // after a malformed suffix (for example an extra ']') must never leak into defaults.
            var defaults = OwnerTokenizer.Tokenize(m.Groups["defaults"].Value, platform, out var allDefaultsValid);
            hasDiagnostics |= !allDefaultsValid;
            section = new Section(name, required, approvals, defaults);
            return true;
        }

        private static bool IsUnparsableSectionHeader(string raw)
            => raw.StartsWith("[", StringComparison.Ordinal) || raw.StartsWith("^[", StringComparison.Ordinal);

        private static bool IsStrictSectionHeader(string raw)
        {
            var index = raw[0] == '^' ? 1 : 0;
            if (index >= raw.Length || raw[index] != '[')
            {
                return false;
            }

            var nameStart = ++index;
            while (index < raw.Length && raw[index] != ']')
            {
                index++;
            }

            if (index == nameStart || index >= raw.Length)
            {
                return false;
            }

            index++;
            if (index < raw.Length && raw[index] == '[')
            {
                var approvalStart = ++index;
                while (index < raw.Length && char.IsDigit(raw[index]))
                {
                    index++;
                }

                if (index == approvalStart || index >= raw.Length || raw[index] != ']')
                {
                    return false;
                }

                index++;
            }

            if (index == raw.Length)
            {
                return true;
            }

            if (!char.IsWhiteSpace(raw[index]))
            {
                return false;
            }

            for (; index < raw.Length; index++)
            {
                if (!IsStrictSectionOwnerCharacter(raw[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStrictSectionOwnerCharacter(char character)
        {
            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character is '@' or '.' or '-' or '/')
            {
                return true;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.ConnectorPunctuation;
        }

        /// <summary>
        /// Compiles a CODEOWNERS-style glob into a deterministic matcher.
        /// Supports **, *, ?, rooted paths, and trailing slash semantics.
        /// Both platforms support escaped literals; GitLab additionally supports shell-style character classes.
        /// Matching is deterministic and bounded by the pattern and path lengths, without regex backtracking.
        /// </summary>
        private static GlobPattern? CompileGlob(string pattern, Platform platform, bool includeDescendants)
            => GlobPattern.Compile(pattern, platform, includeDescendants);

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

        private enum SegmentTokenType
        {
            Literal,
            AnyCharacter,
            Star,
            CharacterClass
        }

        private readonly struct SegmentToken
        {
            private readonly SegmentTokenType _type;
            private readonly char _literal;
            private readonly GlobCharacterClass? _characterClass;

            private SegmentToken(SegmentTokenType type, char literal = default, GlobCharacterClass? characterClass = null)
            {
                _type = type;
                _literal = literal;
                _characterClass = characterClass;
            }

            public static SegmentToken Star { get; } = new(SegmentTokenType.Star);

            public static SegmentToken AnyCharacter { get; } = new(SegmentTokenType.AnyCharacter);

            public bool IsStar => _type == SegmentTokenType.Star;

            public static SegmentToken Literal(char value) => new(SegmentTokenType.Literal, literal: value);

            public static SegmentToken CharacterClass(GlobCharacterClass value) => new(SegmentTokenType.CharacterClass, characterClass: value);

            public bool Matches(char value)
                => _type == SegmentTokenType.AnyCharacter ||
                   (_type == SegmentTokenType.Literal && value == _literal) ||
                   (_type == SegmentTokenType.CharacterClass && _characterClass!.Matches(value));
        }

        private readonly struct CharacterClassAtom
        {
            public CharacterClassAtom(char value, bool escaped)
            {
                Value = value;
                Escaped = escaped;
            }

            public char Value { get; }

            public bool Escaped { get; }
        }

        private readonly struct CharacterRange
        {
            public CharacterRange(char start, char end)
            {
                Start = start;
                End = end;
            }

            public char Start { get; }

            public char End { get; }
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

        private static class OwnerTokenizer
        {
            public static string[] Tokenize(string segment, Platform platform, out bool allValid)
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
                    if (platform == Platform.GitHub)
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
                    else if (!ExtractGitLabOwners(token, owners, uniqueOwners))
                    {
                        allValid = false;
                    }
                }

                return owners.Count == 0 ? [] : owners.ToArray();
            }

            private static void AddUnique(List<string> owners, HashSet<string> uniqueOwners, string owner)
            {
                if (uniqueOwners.Add(owner))
                {
                    owners.Add(owner);
                }
            }

            private static bool IsValidGitHubOwner(string token)
                => IsValidGitHubNamespaceReference(token) || IsWholeEmailReference(token);

            private static bool ExtractGitLabOwners(string token, List<string> owners, HashSet<string> uniqueOwners)
            {
                // Keep the overwhelmingly common canonical forms allocation-light.
                if (IsValidNamespaceReference(token) || IsValidGitLabRole(token))
                {
                    AddUnique(owners, uniqueOwners, token);
                    return true;
                }

                // GitLab extracts references from surrounding punctuation instead of returning
                // the entire token verbatim (for example "(@team)" becomes "@team").
                var foundReference = false;
                var searchStart = 0;
                string? reference;
                while (TryExtractNamespaceReference(token, searchStart, out reference, out searchStart))
                {
                    AddUnique(owners, uniqueOwners, reference);
                    foundReference = true;
                }

                var roleMatches = GitLabRoleReferenceRegex.Matches(token);
                for (var i = 0; i < roleMatches.Count; i++)
                {
                    var roleMatch = roleMatches[i];
                    AddUnique(owners, uniqueOwners, roleMatch.Value);
                    foundReference = true;
                }

                searchStart = 0;
                while (TryExtractGitLabEmailReference(token, searchStart, out var emailStart, out var emailEnd, out searchStart))
                {
                    var email = emailStart == 0 && emailEnd == token.Length
                                    ? token
                                    : token.Substring(emailStart, emailEnd - emailStart);
                    // GitLab's permissive email expression can overlap a namespace reference
                    // (for example "(@team"). Such a value cannot resolve as an email, while
                    // the namespace extracted independently can resolve, so keep only the latter.
                    if (!ContainsNamespaceReference(email))
                    {
                        AddUnique(owners, uniqueOwners, email);
                        foundReference = true;
                    }
                }

                return foundReference;
            }

            private static bool ContainsNamespaceReference(string value)
                => TryExtractNamespaceReference(value, 0, out _, out _);

            private static bool IsValidGitLabRole(string token)
            {
                if (!token.StartsWith("@@", StringComparison.Ordinal) || token.Length <= 2)
                {
                    return false;
                }

                var role = token.Substring(2);
                return role.Equals("developer", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("developers", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("maintainer", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("maintainers", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("owner", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("owners", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsValidNamespaceReference(string token)
                => token.Length > 1 &&
                   token[0] == '@' &&
                   token[1] != '@' &&
                   IsValidNamespace(token, 1, token.Length);

            private static bool IsValidGitHubNamespaceReference(string token)
            {
                if (token.Length <= 1 || token[0] != '@' || token[1] == '@')
                {
                    return false;
                }

                var slash = token.IndexOf('/');
                if (slash < 0)
                {
                    return IsValidGitHubIdentifier(token, 1, token.Length);
                }

                return token.IndexOf('/', slash + 1) < 0 &&
                       IsValidGitHubIdentifier(token, 1, slash) &&
                       IsValidGitHubIdentifier(token, slash + 1, token.Length);
            }

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

            private static bool IsValidNamespace(string value, int start, int end)
            {
                var segmentStart = start;
                for (var i = start; i < end; i++)
                {
                    var character = value[i];
                    if (character == '/')
                    {
                        if (i == segmentStart || !IsNamespaceEnd(value[i - 1]))
                        {
                            return false;
                        }

                        segmentStart = i + 1;
                    }
                    else if ((i == segmentStart && !IsNamespaceStart(character)) || !IsNamespaceCharacter(character))
                    {
                        return false;
                    }
                }

                return segmentStart < end && IsNamespaceEnd(value[end - 1]);
            }

            private static bool TryExtractNamespaceReference(
                string token,
                int searchStart,
                [NotNullWhen(true)] out string? reference,
                out int nextSearchStart)
            {
                for (var atIndex = token.IndexOf('@', searchStart); atIndex >= 0; atIndex = token.IndexOf('@', atIndex + 1))
                {
                    nextSearchStart = atIndex + 1;
                    if ((atIndex > 0 && (IsWordCharacter(token[atIndex - 1]) || token[atIndex - 1] == '@')) ||
                        atIndex + 1 >= token.Length ||
                        token[atIndex + 1] == '@' ||
                        !IsNamespaceStart(token[atIndex + 1]))
                    {
                        continue;
                    }

                    var segmentStart = atIndex + 1;
                    var lastValidEnd = -1;
                    for (var i = segmentStart; i < token.Length; i++)
                    {
                        var character = token[i];
                        if (character == '/')
                        {
                            if (i == segmentStart || lastValidEnd != i)
                            {
                                break;
                            }

                            segmentStart = i + 1;
                            continue;
                        }

                        if ((i == segmentStart && !IsNamespaceStart(character)) || !IsNamespaceCharacter(character))
                        {
                            break;
                        }

                        if (IsNamespaceEnd(character))
                        {
                            lastValidEnd = i + 1;
                        }
                    }

                    if (lastValidEnd > atIndex + 1)
                    {
                        reference = token.Substring(atIndex, lastValidEnd - atIndex);
                        nextSearchStart = lastValidEnd;
                        return true;
                    }
                }

                reference = null;
                nextSearchStart = token.Length;
                return false;
            }

            private static bool IsWholeEmailReference(string token)
                => TryExtractEmailReference(token, out var reference) &&
                   reference.Length == token.Length;

            private static bool TryExtractGitLabEmailReference(
                string token,
                int searchStart,
                out int referenceStart,
                out int referenceEnd,
                out int nextSearchStart)
            {
                for (var atIndex = token.IndexOf('@', searchStart); atIndex >= 0; atIndex = token.IndexOf('@', atIndex + 1))
                {
                    var localStart = atIndex - 1;
                    var localLength = 0;
                    while (localStart >= searchStart &&
                           localLength < 100 &&
                           token[localStart] != '@' &&
                           !char.IsWhiteSpace(token[localStart]))
                    {
                        localStart--;
                        localLength++;
                    }

                    localStart++;
                    if (localLength == 0)
                    {
                        continue;
                    }

                    var domainEnd = atIndex + 1;
                    var domainLimit = Math.Min(token.Length, domainEnd + 255);
                    var lastWordEnd = -1;
                    while (domainEnd < domainLimit && token[domainEnd] != '@' && !char.IsWhiteSpace(token[domainEnd]))
                    {
                        if (IsRegexWordCharacter(token[domainEnd]))
                        {
                            lastWordEnd = domainEnd + 1;
                        }

                        domainEnd++;
                    }

                    if (lastWordEnd <= atIndex + 1)
                    {
                        continue;
                    }

                    referenceStart = localStart;
                    referenceEnd = lastWordEnd;
                    nextSearchStart = lastWordEnd;
                    return true;
                }

                referenceStart = -1;
                referenceEnd = -1;
                nextSearchStart = token.Length;
                return false;
            }

            private static bool TryExtractEmailReference(string token, [NotNullWhen(true)] out string? reference)
            {
                for (var atIndex = token.IndexOf('@'); atIndex >= 0; atIndex = token.IndexOf('@', atIndex + 1))
                {
                    if (atIndex == 0)
                    {
                        continue;
                    }

                    var localStart = atIndex - 1;
                    while (localStart >= 0 && IsEmailLocalCharacter(token[localStart]))
                    {
                        localStart--;
                    }

                    localStart++;
                    var localLength = atIndex - localStart;
                    if (localLength is < 1 or > 100)
                    {
                        continue;
                    }

                    var domainEnd = atIndex + 1;
                    var lastValidDomainEnd = -1;
                    while (domainEnd < token.Length && IsEmailDomainCharacter(token[domainEnd]))
                    {
                        if (IsWordCharacter(token[domainEnd]))
                        {
                            lastValidDomainEnd = domainEnd + 1;
                        }

                        domainEnd++;
                    }

                    if (lastValidDomainEnd <= atIndex + 1 || lastValidDomainEnd - atIndex - 1 > 255)
                    {
                        continue;
                    }

                    reference = token.Substring(localStart, lastValidDomainEnd - localStart);
                    return true;
                }

                reference = null;
                return false;
            }

            private static bool IsNamespaceStart(char character)
                => IsAsciiLetterOrDigit(character) || character is '_' or '.';

            private static bool IsNamespaceCharacter(char character)
                => IsNamespaceStart(character) || character == '-';

            private static bool IsNamespaceEnd(char character)
                => IsAsciiLetterOrDigit(character) || character is '_' or '-';

            private static bool IsEmailLocalCharacter(char character)
                => IsAsciiLetterOrDigit(character) || ".!#$%&'*+/=?^_`{|}~-".IndexOf(character) >= 0;

            private static bool IsEmailDomainCharacter(char character)
                => IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_';

            private static bool IsWordCharacter(char character)
                => char.IsLetterOrDigit(character) || character == '_';

            private static bool IsRegexWordCharacter(char character)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return true;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.ConnectorPunctuation;
            }

            private static bool IsAsciiLetterOrDigit(char character)
                => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
        }

        private sealed class GlobPattern
        {
            private readonly GlobPathSegment[] _segments;

            private GlobPattern(GlobPathSegment[] segments)
            {
                _segments = segments;
            }

            public static GlobPattern? Compile(string pattern, Platform platform, bool includeDescendants)
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
                    if (rawSegments[i] == "**")
                    {
                        // A terminal /** means contents below the preceding directory and must
                        // consume at least one path segment. Middle globstars may consume none.
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
            private readonly SegmentToken[] _tokens;

            private SegmentPattern(SegmentToken[] tokens)
            {
                _tokens = tokens;
            }

            public static bool TryCompile(string pattern, Platform platform, [NotNullWhen(true)] out SegmentPattern? segment)
            {
                var tokens = new List<SegmentToken>(pattern.Length);
                for (var i = 0; i < pattern.Length; i++)
                {
                    var character = pattern[i];
                    if (character == '\\')
                    {
                        // Both gitignore-style GitHub patterns and GitLab File.fnmatch patterns use
                        // a backslash to escape the following character. A trailing backslash is invalid.
                        if (i + 1 >= pattern.Length)
                        {
                            segment = null;
                            return false;
                        }

                        tokens.Add(SegmentToken.Literal(pattern[++i]));
                    }
                    else if (character == '*')
                    {
                        if (tokens.Count == 0 || !tokens[tokens.Count - 1].IsStar)
                        {
                            tokens.Add(SegmentToken.Star);
                        }
                    }
                    else if (character == '?')
                    {
                        tokens.Add(SegmentToken.AnyCharacter);
                    }
                    else if (platform == Platform.GitLab && character == '[')
                    {
                        var result = TryParseCharacterClass(pattern, i, out var closingBracket, out var characterClass);
                        if (result == CharacterClassParseResult.Invalid)
                        {
                            segment = null;
                            return false;
                        }

                        if (result == CharacterClassParseResult.Success)
                        {
                            tokens.Add(SegmentToken.CharacterClass(characterClass!));
                            i = closingBracket;
                        }
                        else
                        {
                            tokens.Add(SegmentToken.Literal(character));
                        }
                    }
                    else
                    {
                        tokens.Add(SegmentToken.Literal(character));
                    }
                }

                segment = new SegmentPattern(tokens.ToArray());
                return true;
            }

            public bool IsMatch(string path, int start, int end)
            {
                var tokenIndex = 0;
                var pathIndex = start;
                var starTokenIndex = -1;
                var starPathIndex = -1;

                while (pathIndex < end)
                {
                    if (tokenIndex < _tokens.Length && _tokens[tokenIndex].IsStar)
                    {
                        starTokenIndex = tokenIndex++;
                        starPathIndex = pathIndex;
                        continue;
                    }

                    if (tokenIndex < _tokens.Length && _tokens[tokenIndex].Matches(path[pathIndex]))
                    {
                        tokenIndex++;
                        pathIndex++;
                        continue;
                    }

                    if (starTokenIndex < 0)
                    {
                        return false;
                    }

                    tokenIndex = starTokenIndex + 1;
                    pathIndex = ++starPathIndex;
                }

                while (tokenIndex < _tokens.Length && _tokens[tokenIndex].IsStar)
                {
                    tokenIndex++;
                }

                return tokenIndex == _tokens.Length;
            }

            private static CharacterClassParseResult TryParseCharacterClass(
                string pattern,
                int openingBracket,
                out int closingBracket,
                [NotNullWhen(true)] out GlobCharacterClass? characterClass)
            {
                closingBracket = -1;
                characterClass = null;
                var contentStart = openingBracket + 1;
                var negated = contentStart < pattern.Length && pattern[contentStart] is '!' or '^';
                var atomStart = negated ? contentStart + 1 : contentStart;
                var searchStart = atomStart;

                // A closing bracket immediately after the optional negation is a literal member.
                if (searchStart < pattern.Length && pattern[searchStart] == ']')
                {
                    searchStart++;
                }

                for (var i = searchStart; i < pattern.Length; i++)
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

                var atoms = new List<CharacterClassAtom>();
                for (var i = atomStart; i < closingBracket; i++)
                {
                    if (pattern[i] == '\\' && i + 1 < closingBracket)
                    {
                        atoms.Add(new CharacterClassAtom(pattern[++i], escaped: true));
                    }
                    else
                    {
                        atoms.Add(new CharacterClassAtom(pattern[i], escaped: false));
                    }
                }

                if (atoms.Count == 0)
                {
                    return CharacterClassParseResult.Invalid;
                }

                var ranges = new List<CharacterRange>(atoms.Count);
                for (var i = 0; i < atoms.Count; i++)
                {
                    if (i + 2 < atoms.Count && atoms[i + 1].Value == '-' && !atoms[i + 1].Escaped)
                    {
                        if (atoms[i].Value > atoms[i + 2].Value)
                        {
                            return CharacterClassParseResult.Invalid;
                        }

                        ranges.Add(new CharacterRange(atoms[i].Value, atoms[i + 2].Value));
                        i += 2;
                    }
                    else
                    {
                        ranges.Add(new CharacterRange(atoms[i].Value, atoms[i].Value));
                    }
                }

                characterClass = new GlobCharacterClass(negated, ranges.ToArray());
                return CharacterClassParseResult.Success;
            }
        }

        private sealed class GlobCharacterClass
        {
            private readonly bool _negated;
            private readonly CharacterRange[] _ranges;

            public GlobCharacterClass(bool negated, CharacterRange[] ranges)
            {
                _negated = negated;
                _ranges = ranges;
            }

            public bool Matches(char value)
            {
                foreach (var range in _ranges)
                {
                    if (value >= range.Start && value <= range.End)
                    {
                        return !_negated;
                    }
                }

                return _negated;
            }
        }

        private sealed class Section
        {
            private readonly List<Entry> _entries = new();
            private Entry[]? _cache;
            private bool _replaceDuplicatePatterns;

            public Section(string name, bool required, int approvalCount, string[] defaultOwners)
            {
                Name = name;
                Required = required;
                ApprovalCount = approvalCount;
                DefaultOwners = defaultOwners.Length == 0 ? [] : defaultOwners;
            }

            public string Name { get; }

            public bool Required { get; private set; }

            public int ApprovalCount { get; private set; }

            public string[] DefaultOwners { get; }

            public static Section CreateUnnamed() => new(string.Empty, required: true, approvalCount: 0, defaultOwners: []);

            public void Add(Entry entry, bool replaceDuplicatePattern)
            {
                _replaceDuplicatePatterns |= replaceDuplicatePattern;
                _entries.Add(entry);
            }

            public void MergeMetadata(Section other)
            {
                // Duplicate GitLab sections are combined case-insensitively. The most restrictive
                // requirement wins; matching defaults remain attached to entries from each header.
                Required |= other.Required;
                ApprovalCount = Math.Max(ApprovalCount, other.ApprovalCount);
            }

            public void Seal()
            {
                if (_replaceDuplicatePatterns)
                {
                    // GitLab replaces duplicate normalized patterns and moves the replacement to
                    // the end. Build the reverse-order cache in one pass instead of repeatedly
                    // removing from the middle of the list.
                    var seenPatterns = new HashSet<string>(StringComparer.Ordinal);
                    var cache = new List<Entry>(_entries.Count);
                    for (var i = _entries.Count - 1; i >= 0; i--)
                    {
                        var entry = _entries[i];
                        if (seenPatterns.Add(entry.PatternKey))
                        {
                            cache.Add(entry);
                        }
                    }

                    _cache = cache.ToArray();
                }
                else
                {
                    _cache = _entries.AsEnumerable().Reverse().ToArray();
                }

                _entries.Clear();
            }

            /// <summary>
            /// GitHub evaluation: exclusion rules are unsupported and ignored, section default owners don't
            /// exist, and the caller stops at the first (i.e. last in file order) matching rule.
            /// </summary>
            public bool TryMatchGitHub(string path, [NotNullWhen(true)] out string[]? owners)
            {
                var rules = _cache ?? [];

                foreach (var rule in rules)
                {
                    if (!rule.Match(path))
                    {
                        continue;
                    }

                    owners = rule.Owners;
                    return true;
                }

                owners = null;
                return false;
            }

            /// <summary>
            /// GitLab evaluation: rules are evaluated in file order within the section; the last matching
            /// entry wins, an exclusion exempts the path for the whole section (later rules cannot
            /// re-include it), and entries without owners inherit the section default owners.
            /// </summary>
            public bool TryMatchGitLab(string path, [NotNullWhen(true)] out string[]? owners)
            {
                var rules = _cache ?? [];
                string[]? matchedOwners = null;
                var excluded = false;

                // The cache holds entries in reverse file order, so iterating from the end evaluates
                // rules in file order: each match overwrites the previous one, leaving the last
                // matching rule's owners.
                for (var i = rules.Length - 1; i >= 0; i--)
                {
                    var rule = rules[i];
                    if (!rule.Match(path))
                    {
                        continue;
                    }

                    if (rule.IsExclusion)
                    {
                        // Exclusions are terminal for the section: later rules cannot re-include the path.
                        excluded = true;
                        break;
                    }

                    matchedOwners = rule.Owners;
                }

                if (excluded || matchedOwners is null || matchedOwners.Length == 0)
                {
                    owners = null;
                    return false;
                }

                owners = matchedOwners;
                return true;
            }
        }

        private sealed class Entry
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

            public static Entry? Parse(string raw, Platform platform, string[] defaultOwners, out bool hasDiagnostics)
            {
                hasDiagnostics = false;
                if (platform == Platform.GitHub && raw.StartsWith("\\#"))
                {
                    // GitHub does not support escaping a leading #; the line is invalid, not a
                    // literal pattern beginning with #.
                    return null;
                }

                // Strip inline comments for GitHub. GitLab treats everything after # as data (inline comments unsupported).
                var idxHash = platform == Platform.GitHub ? FindUnescapedCharacter(raw, '#') : -1;
                var effective = idxHash >= 0 && platform == Platform.GitHub ? raw.Substring(0, idxHash).TrimEnd() : raw;
                if (string.IsNullOrWhiteSpace(effective))
                {
                    return null;
                }

                // 2. Tokenise on unescaped whitespace. Both platforms support escaped literals
                // in patterns; owner tokens themselves are not unescaped.
                string patternToken;
                string ownersSegment;
                bool hasExplicitOwners;
                SplitEscapedEntry(effective, out patternToken, out ownersSegment, out hasExplicitOwners);

                // 3. Pattern & exclusion
                var isExclusion = platform == Platform.GitLab && patternToken.StartsWith("!");
                if (isExclusion)
                {
                    patternToken = patternToken.Substring(1, patternToken.Length - 1);
                }

                if (platform == Platform.GitHub && IsUnsupportedGitHubPattern(patternToken))
                {
                    // GitHub skips invalid CODEOWNERS lines instead of interpreting unsupported
                    // gitignore constructs as literal file names.
                    return null;
                }

                if (patternToken.Length == 0)
                {
                    return null;
                }

                // 4. Owners. GitLab exclusions deliberately ignore any trailing owner text.
                string[] owners;
                var allOwnersValid = true;
                if (isExclusion)
                {
                    owners = [];
                }
                else
                {
                    owners = OwnerTokenizer.Tokenize(ownersSegment, platform, out allOwnersValid);
                }

                hasDiagnostics = !allOwnersValid;
                if (platform == Platform.GitHub && hasDiagnostics)
                {
                    // GitHub skips a whole rule containing a malformed owner token.
                    return null;
                }

                if (platform == Platform.GitLab && !isExclusion && !hasExplicitOwners && defaultOwners.Length > 0)
                {
                    owners = defaultOwners;
                }

                if (platform == Platform.GitLab && !isExclusion && owners.Length == 0)
                {
                    // GitLab keeps an ownerless rule because it can intentionally auto-approve a
                    // path, but reports the missing owner as a parsing diagnostic.
                    hasDiagnostics = true;
                }

                // GitHub owns the contents of directories matched by wildcard-free patterns (e.g.
                // `**/logs`); GitLab requires an explicit trailing slash for directory ownership.
                var isDirectoryPattern = platform == Platform.GitHub && IsDirectoryPattern(patternToken);

                // 5. Compile the glob
                var glob = CompileGlob(patternToken, platform, includeDescendants: isDirectoryPattern);
                if (glob is null)
                {
                    return null;
                }

                var patternKey = platform == Platform.GitLab ? NormalizeGitLabPatternKey(patternToken) : patternToken;
                return new Entry(glob, patternKey, isExclusion, owners);
            }

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

            private static string NormalizeGitLabPatternKey(string patternToken)
            {
                if (patternToken == "*")
                {
                    return "/**/*";
                }

                var normalizedToken = NormalizeGitLabEscapes(patternToken);
                var normalized = normalizedToken.StartsWith("/") ? normalizedToken : "/**/" + normalizedToken;
                return normalized.EndsWith("/") ? normalized + "**/*" : normalized;
            }

            private static string NormalizeGitLabEscapes(string patternToken)
            {
                StringBuilder? builder = null;
                var copyStart = 0;
                for (var i = 0; i + 1 < patternToken.Length; i++)
                {
                    var unescape = i == 0 && patternToken[i] == '\\' && patternToken[i + 1] == '#';
                    unescape |= patternToken[i] == '\\' && char.IsWhiteSpace(patternToken[i + 1]);
                    if (!unescape)
                    {
                        continue;
                    }

                    builder ??= new StringBuilder(patternToken.Length);
                    builder.Append(patternToken, copyStart, i - copyStart);
                    copyStart = i + 1;
                }

                if (builder is null)
                {
                    return patternToken;
                }

                builder.Append(patternToken, copyStart, patternToken.Length - copyStart);
                return builder.ToString();
            }

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

            public bool Match(string path) => _glob.IsMatch(path);
        }
    }
}
