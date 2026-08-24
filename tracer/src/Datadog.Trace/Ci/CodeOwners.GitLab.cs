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

        /// <summary>
        /// Parses a GitLab section header, its name, and its default owners.
        /// </summary>
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

            // Use only the owner text matched by the header. Ignore text after a bad suffix.
            var defaults = GitLabOwnerTokenizer.TokenizeGitLab(m.Groups["defaults"].Value, out var allDefaultsValid);
            hasDiagnostics |= !allDefaultsValid;
            section = new GitLabSection(name, defaults);
            return true;
        }

        /// <summary>
        /// Checks whether a line looks like a section header but could not be parsed.
        /// </summary>
        private static bool IsUnparsableSectionHeader(string raw)
            => raw.StartsWith("[", StringComparison.Ordinal) || raw.StartsWith("^[", StringComparison.Ordinal);

        /// <summary>
        /// Checks whether a section header follows GitLab's strict syntax.
        /// </summary>
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

        /// <summary>
        /// Checks whether a character is allowed in the owner part of a section header.
        /// </summary>
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
        /// Finds GitLab users, groups, roles, and emails inside owner text.
        /// </summary>
        private static class GitLabOwnerTokenizer
        {
            /// <summary>
            /// Extracts valid owners, removes duplicates, and reports text without a valid owner.
            /// </summary>
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
                foreach (var token in segment.Split(OwnerSeparators, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!ExtractGitLabOwners(token, owners, uniqueOwners))
                    {
                        allValid = false;
                    }
                }

                return owners.Count == 0 ? [] : owners.ToArray();
            }

            /// <summary>
            /// Finds every valid GitLab owner reference inside one token.
            /// </summary>
            private static bool ExtractGitLabOwners(string token, List<string> owners, HashSet<string> uniqueOwners)
            {
                // Handle common complete references without extra parsing.
                if (IsWholeNamespaceReference(token) || IsValidGitLabRole(token))
                {
                    AddUniqueOwner(owners, uniqueOwners, token);
                    return true;
                }

                // GitLab accepts references inside punctuation, such as "(@team)".
                var foundReference = false;
                var searchStart = 0;
                while (TryFindNamespaceReference(token, searchStart, out var referenceStart, out var referenceEnd, out searchStart))
                {
                    var reference = token.Substring(referenceStart, referenceEnd - referenceStart);
                    AddUniqueOwner(owners, uniqueOwners, reference);
                    foundReference = true;
                }

                var roleMatches = GitLabRoleReferenceRegex.Matches(token);
                for (var i = 0; i < roleMatches.Count; i++)
                {
                    var roleMatch = roleMatches[i];
                    AddUniqueOwner(owners, uniqueOwners, roleMatch.Value);
                    foundReference = true;
                }

                searchStart = 0;
                while (TryExtractGitLabEmailReference(token, searchStart, out var emailStart, out var emailEnd, out searchStart))
                {
                    var email = emailStart == 0 && emailEnd == token.Length
                                    ? token
                                    : token.Substring(emailStart, emailEnd - emailStart);
                    // Do not add an email when the same text contains a valid group reference.
                    if (!ContainsNamespaceReference(email))
                    {
                        AddUniqueOwner(owners, uniqueOwners, email);
                        foundReference = true;
                    }
                }

                return foundReference;
            }

            /// <summary>
            /// Checks whether text contains a GitLab user or group reference.
            /// </summary>
            private static bool ContainsNamespaceReference(string value)
                => TryFindNamespaceReference(value, 0, out _, out _, out _);

            /// <summary>
            /// Checks whether a token is one of GitLab's supported role references.
            /// </summary>
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

            /// <summary>
            /// Checks whether the full token is one GitLab user or group reference.
            /// </summary>
            private static bool IsWholeNamespaceReference(string token)
                => TryFindNamespaceReference(token, 0, out var start, out var end, out _) && start == 0 && end == token.Length;

            /// <summary>
            /// Finds the next GitLab user or group reference starting at the requested position.
            /// </summary>
            private static bool TryFindNamespaceReference(
                string token,
                int searchStart,
                out int referenceStart,
                out int referenceEnd,
                out int nextSearchStart)
            {
                // Find each @ and reject it when the characters around it make it part of another word.
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

                    // Read slash-separated namespace parts and remember the last valid end.
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

            /// <summary>
            /// Finds the next email-like owner reference while enforcing GitLab's length limits.
            /// </summary>
            private static bool TryExtractGitLabEmailReference(
                string token,
                int searchStart,
                out int referenceStart,
                out int referenceEnd,
                out int nextSearchStart)
            {
                for (var atIndex = token.IndexOf('@', searchStart); atIndex >= 0; atIndex = token.IndexOf('@', atIndex + 1))
                {
                    // Read the local part backwards from @.
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

                    // Read the domain forwards and keep the last valid word character.
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

            /// <summary>
            /// Checks whether a character can start a GitLab namespace segment.
            /// </summary>
            private static bool IsNamespaceStart(char character)
                => IsAsciiLetterOrDigit(character) || character is '_' or '.';

            /// <summary>
            /// Checks whether a character can appear inside a GitLab namespace segment.
            /// </summary>
            private static bool IsNamespaceCharacter(char character)
                => IsNamespaceStart(character) || character == '-';

            /// <summary>
            /// Checks whether a character can end a GitLab namespace segment.
            /// </summary>
            private static bool IsNamespaceEnd(char character)
                => IsAsciiLetterOrDigit(character) || character is '_' or '-';

            /// <summary>
            /// Checks whether a character is a Unicode word character used as a left boundary.
            /// </summary>
            private static bool IsWordCharacter(char character)
                => char.IsLetterOrDigit(character) || character == '_';

            /// <summary>
            /// Applies the Unicode word-character rules used by GitLab's email parser.
            /// </summary>
            private static bool IsRegexWordCharacter(char character)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return true;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.ConnectorPunctuation;
            }
        }

        /// <summary>
        /// Stores GitLab rules grouped into independent sections.
        /// </summary>
        /// <remarks>
        /// See <see href="https://docs.gitlab.com/user/project/codeowners/reference/#sections">GitLab CODEOWNERS section rules</see>.
        /// </remarks>
        private sealed class GitLabDocument : Document
        {
            private readonly GitLabSection[] _sections;

            /// <summary>
            /// Initializes a new instance of the <see cref="GitLabDocument"/> class from parsed sections.
            /// </summary>
            private GitLabDocument(GitLabSection[] sections)
            {
                _sections = sections;
            }

            /// <summary>
            /// Parses GitLab sections and rules and counts invalid input.
            /// </summary>
            public static GitLabDocument Parse(IEnumerable<string> lines, out int parsingDiagnosticsCount)
            {
                parsingDiagnosticsCount = 0;
                var sections = new List<GitLabSection>();
                // Rules before the first header belong to the unnamed section.
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
                            // Repeated section names add rules to the first section with that name.
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
                        // A malformed header is invalid and must not become a path rule.
                        parsingDiagnosticsCount++;
                        continue;
                    }

                    if (raw[0] == '#')
                    {
                        // Comments do not assign owners to paths.
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

                // Finish each section after all repeated definitions have been joined.
                foreach (var section in sections)
                {
                    section.Seal();
                }

                return new GitLabDocument(sections.ToArray());
            }

            /// <summary>
            /// Matches each section separately and combines the owners from every matching section.
            /// </summary>
            public override IEnumerable<string> Match(string path)
            {
                // Create the set only after the first section matches.
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

        /// <summary>
        /// Stores the rules and default owners for one GitLab section.
        /// </summary>
        private sealed class GitLabSection
        {
            private readonly List<Entry> _entries = new();
            private Entry[]? _cache;

            /// <summary>
            /// Initializes a new instance of the <see cref="GitLabSection"/> class.
            /// </summary>
            public GitLabSection(string name, string[] defaultOwners)
            {
                Name = name;
                DefaultOwners = defaultOwners.Length == 0 ? [] : defaultOwners;
            }

            public string Name { get; }

            public string[] DefaultOwners { get; }

            /// <summary>
            /// Creates the section used for rules before the first named header.
            /// </summary>
            public static GitLabSection CreateUnnamed() => new(string.Empty, []);

            /// <summary>
            /// Adds a parsed rule to this section.
            /// </summary>
            public void Add(Entry entry) => _entries.Add(entry);

            /// <summary>
            /// Prepares the section for matching by keeping the last rule for each exact pattern.
            /// </summary>
            public void Seal()
            {
                var seenPatterns = new HashSet<string>(StringComparer.Ordinal);
                var cache = new List<Entry>(_entries.Count);
                // Walk backwards because later rules replace earlier rules with the same pattern.
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

            /// <summary>
            /// Returns the owners selected by this section, unless a matching exclusion removes the path.
            /// </summary>
            /// <remarks>
            /// See <see href="https://docs.gitlab.com/user/project/codeowners/reference/#exclusion-patterns">GitLab CODEOWNERS exclusion rules</see>.
            /// </remarks>
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
                        // An exclusion applies only to this section.
                        owners = null;
                        return false;
                    }

                    // Rules are stored last-first, so the first normal match wins.
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
            /// <summary>
            /// Parses one GitLab rule, applies section defaults, and compiles its path pattern.
            /// </summary>
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
                    // A rule without owners inherits the current section defaults.
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

            /// <summary>
            /// Normalizes equivalent GitLab patterns so later duplicates can replace earlier ones.
            /// </summary>
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

            /// <summary>
            /// Removes GitLab escapes for a leading hash and for whitespace.
            /// </summary>
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
