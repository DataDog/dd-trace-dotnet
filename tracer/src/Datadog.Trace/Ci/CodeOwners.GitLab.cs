// <copyright file="CodeOwners.GitLab.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Datadog.Trace.Ci
{
    internal sealed partial class CodeOwners
    {
        private static readonly Regex SectionHeaderRegex = new(
            @"^\s*(\^)?\[(?<name>.*?)\](?:\[(?<cnt>[\s\d]*)\])?(?<defaults>\s*[@\w.\-/\s]*)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex GitLabRoleReferenceRegex = new(
            @"(?<![\w@])@@(?:developer|maintainer|owner)s?(?=\s|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static bool TryParseGitLabSectionHeader(
            string raw,
            [NotNullWhen(true)] out GitLabSection? section,
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
            hasDiagnostics = name.Length == 0 ||
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
            var defaults = GitLabOwnerTokenizer.TokenizeGitLab(m.Groups["defaults"].Value, out var allDefaultsValid);
            hasDiagnostics |= !allDefaultsValid;
            section = new GitLabSection(name, defaults);
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

        private static class GitLabOwnerTokenizer
        {
            public static string[] TokenizeGitLab(string segment, out bool allValid)
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
                    if (!ExtractGitLabOwners(token, owners, uniqueOwners))
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

            private static bool ExtractGitLabOwners(string token, List<string> owners, HashSet<string> uniqueOwners)
            {
                // Keep the overwhelmingly common canonical forms allocation-light.
                if (IsWholeNamespaceReference(token) || IsValidGitLabRole(token))
                {
                    AddUnique(owners, uniqueOwners, token);
                    return true;
                }

                // GitLab extracts references from surrounding punctuation instead of returning
                // the entire token verbatim (for example "(@team)" becomes "@team").
                var foundReference = false;
                var searchStart = 0;
                while (TryFindNamespaceReference(token, searchStart, out var referenceStart, out var referenceEnd, out searchStart))
                {
                    var reference = token.Substring(referenceStart, referenceEnd - referenceStart);
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
                => TryFindNamespaceReference(value, 0, out _, out _, out _);

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

            private static bool IsWholeNamespaceReference(string token)
                => TryFindNamespaceReference(token, 0, out var start, out var end, out _) && start == 0 && end == token.Length;

            private static bool TryFindNamespaceReference(
                string token,
                int searchStart,
                out int referenceStart,
                out int referenceEnd,
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
                        referenceStart = atIndex;
                        referenceEnd = lastValidEnd;
                        nextSearchStart = lastValidEnd;
                        return true;
                    }
                }

                referenceStart = -1;
                referenceEnd = -1;
                nextSearchStart = token.Length;
                return false;
            }

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

            private static bool IsNamespaceStart(char character)
                => IsAsciiLetterOrDigit(character) || character is '_' or '.';

            private static bool IsNamespaceCharacter(char character)
                => IsNamespaceStart(character) || character == '-';

            private static bool IsNamespaceEnd(char character)
                => IsAsciiLetterOrDigit(character) || character is '_' or '-';

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

        private sealed class GitLabDocument : Document
        {
            private readonly GitLabSection[] _sections;

            private GitLabDocument(GitLabSection[] sections)
            {
                _sections = sections;
            }

            public static GitLabDocument Parse(IEnumerable<string> lines, out int parsingDiagnosticsCount)
            {
                parsingDiagnosticsCount = 0;
                var sections = new List<GitLabSection>();
                var current = GitLabSection.CreateUnnamed();
                var currentDefaultOwners = current.DefaultOwners;
                var namedSections = new Dictionary<string, GitLabSection>(StringComparer.OrdinalIgnoreCase);
                sections.Add(current);

                foreach (var line in lines)
                {
                    var raw = line.Trim();
                    if (raw.Length == 0)
                    {
                        continue;
                    }

                    if (TryParseGitLabSectionHeader(raw, out var newSection, out var sectionHasDiagnostics))
                    {
                        if (sectionHasDiagnostics)
                        {
                            parsingDiagnosticsCount++;
                        }

                        currentDefaultOwners = newSection.DefaultOwners;
                        if (namedSections.TryGetValue(newSection.Name, out var existingSection))
                        {
                            current = existingSection;
                        }
                        else
                        {
                            current = newSection;
                            sections.Add(current);
                            namedSections.Add(current.Name, current);
                        }

                        continue;
                    }

                    if (IsUnparsableSectionHeader(raw))
                    {
                        // GitLab reports malformed header-like lines and skips them rather than
                        // reinterpreting them as path patterns.
                        parsingDiagnosticsCount++;
                        continue;
                    }

                    if (raw[0] == '#')
                    {
                        // GitLab parses owners inside comments for its MR widget, but comments do
                        // not bind those owners to a path and therefore do not affect matching.
                        continue;
                    }

                    var entry = Entry.ParseGitLab(raw, currentDefaultOwners, out var entryHasDiagnostics);
                    if (entry is null)
                    {
                        parsingDiagnosticsCount++;
                    }
                    else
                    {
                        if (entryHasDiagnostics)
                        {
                            parsingDiagnosticsCount++;
                        }

                        current.Add(entry);
                    }
                }

                foreach (var section in sections)
                {
                    section.Seal();
                }

                return new GitLabDocument(sections.ToArray());
            }

            public override IEnumerable<string> Match(string path)
            {
                // GitLab evaluates each section independently and combines their owners.
                // The set is allocated lazily because most paths match at most one section.
                HashSet<string>? owners = null;
                foreach (var section in _sections)
                {
                    if (section.TryMatch(path, out var sectionOwners))
                    {
                        owners ??= new HashSet<string>(StringComparer.Ordinal);
                        foreach (var owner in sectionOwners)
                        {
                            owners.Add(owner);
                        }
                    }
                }

                return owners ?? [];
            }
        }

        private sealed class GitLabSection
        {
            private readonly List<Entry> _entries = new();
            private Entry[]? _cache;

            public GitLabSection(string name, string[] defaultOwners)
            {
                Name = name;
                DefaultOwners = defaultOwners.Length == 0 ? [] : defaultOwners;
            }

            public string Name { get; }

            public string[] DefaultOwners { get; }

            public static GitLabSection CreateUnnamed() => new(string.Empty, []);

            public void Add(Entry entry) => _entries.Add(entry);

            public void Seal()
            {
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
                _entries.Clear();
            }

            public bool TryMatch(string path, [NotNullWhen(true)] out string[]? owners)
            {
                var rules = _cache ?? [];
                string[]? matchedOwners = null;

                foreach (var rule in rules)
                {
                    if (!rule.Match(path))
                    {
                        continue;
                    }

                    if (rule.IsExclusion)
                    {
                        owners = null;
                        return false;
                    }

                    matchedOwners ??= rule.Owners;
                }

                if (matchedOwners is null || matchedOwners.Length == 0)
                {
                    owners = null;
                    return false;
                }

                owners = matchedOwners;
                return true;
            }
        }

        private sealed partial class Entry
        {
            public static Entry? ParseGitLab(string raw, string[] defaultOwners, out bool hasDiagnostics)
            {
                hasDiagnostics = false;
                SplitEscapedEntry(raw, out var patternToken, out var ownersSegment, out var hasExplicitOwners);

                var isExclusion = patternToken.StartsWith("!");
                if (isExclusion)
                {
                    patternToken = patternToken.Substring(1, patternToken.Length - 1);
                }

                if (patternToken.Length == 0)
                {
                    return null;
                }

                var allOwnersValid = true;
                var owners = isExclusion
                                 ? []
                                 : GitLabOwnerTokenizer.TokenizeGitLab(ownersSegment, out allOwnersValid);
                hasDiagnostics = !isExclusion && !allOwnersValid;

                if (!isExclusion && !hasExplicitOwners && defaultOwners.Length > 0)
                {
                    owners = defaultOwners;
                }

                if (!isExclusion && owners.Length == 0)
                {
                    hasDiagnostics = true;
                }

                var glob = GlobPattern.CompileGitLab(patternToken);
                if (glob is null)
                {
                    return null;
                }

                return new Entry(glob, NormalizeGitLabPatternKey(patternToken), isExclusion, owners);
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
        }
    }
}
