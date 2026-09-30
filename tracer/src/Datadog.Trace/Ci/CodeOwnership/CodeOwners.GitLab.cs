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

namespace Datadog.Trace.Ci.CodeOwnership
{
    internal sealed partial class CodeOwners
    {
        private static readonly Regex SectionHeaderRegex = new(
            @"^\s*(\^)?\[(?<name>.*?)\](?:\[(?<approvals>[\s\d]*)\])?(?<defaults>\s*[@\w.\-/\s]*)?",
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
            var headerMatch = SectionHeaderRegex.Match(raw);
            if (!headerMatch.Success)
            {
                section = null;
                hasDiagnostics = false;
                return false;
            }

            var isOptional = headerMatch.Groups[1].Success; // ^ prefix => optional section
            var name = headerMatch.Groups["name"].Value.Trim();
            hasDiagnostics = name.Length == 0 ||
                             !IsStrictSectionHeader(raw);

            if (headerMatch.Groups["approvals"].Success)
            {
                if (int.TryParse(headerMatch.Groups["approvals"].Value, out var approvalCount))
                {
                    if (isOptional && approvalCount > 0)
                    {
                        hasDiagnostics = true;
                    }
                }
                else
                {
                    hasDiagnostics = true;
                }
            }

            // Use only the owner text matched by the header. Ignore text after a bad suffix.
            var defaults = GitLabOwnerTokenizer.TokenizeGitLab(headerMatch.Groups["defaults"].Value, out var allDefaultsValid);
            if (!allDefaultsValid)
            {
                hasDiagnostics = true;
            }

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
                while (TryFindNamespaceReference(token, searchStart, out var reference))
                {
                    AddUniqueOwner(owners, uniqueOwners, reference.GetValue(token));
                    foundReference = true;
                    searchStart = reference.End;
                }

                var roleMatches = GitLabRoleReferenceRegex.Matches(token);
                for (var i = 0; i < roleMatches.Count; i++)
                {
                    var roleMatch = roleMatches[i];
                    AddUniqueOwner(owners, uniqueOwners, roleMatch.Value);
                    foundReference = true;
                }

                searchStart = 0;
                while (TryFindEmailReference(token, searchStart, out var emailReference))
                {
                    var email = emailReference.GetValue(token);
                    // Do not add an email when the same text contains a valid group reference.
                    if (!ContainsNamespaceReference(email))
                    {
                        AddUniqueOwner(owners, uniqueOwners, email);
                        foundReference = true;
                    }

                    searchStart = emailReference.End;
                }

                return foundReference;
            }

            private static bool ContainsNamespaceReference(string value)
                => TryFindNamespaceReference(value, 0, out _);

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
                => TryFindNamespaceReference(token, 0, out var reference) && reference.Start == 0 && reference.End == token.Length;

            private static bool TryFindNamespaceReference(string token, int searchStart, out OwnerReference reference)
            {
                // Find each @ and reject it when the characters around it make it part of another word.
                for (var atIndex = token.IndexOf('@', searchStart); atIndex >= 0; atIndex = token.IndexOf('@', atIndex + 1))
                {
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
                        reference = new OwnerReference(atIndex, lastValidEnd);
                        return true;
                    }
                }

                reference = default;
                return false;
            }

            private static bool TryFindEmailReference(string token, int searchStart, out OwnerReference reference)
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

                    reference = new OwnerReference(localStart, lastWordEnd);
                    return true;
                }

                reference = default;
                return false;
            }

            private static bool IsNamespaceStart(char character)
                => char.IsAsciiLetterOrDigit(character) || character is '_' or '.';

            private static bool IsNamespaceCharacter(char character)
                => IsNamespaceStart(character) || character == '-';

            private static bool IsNamespaceEnd(char character)
                => char.IsAsciiLetterOrDigit(character) || character is '_' or '-';

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

            private readonly struct OwnerReference
            {
                internal OwnerReference(int start, int end)
                {
                    Start = start;
                    End = end;
                }

                internal int Start { get; }

                internal int End { get; }

                internal string GetValue(string token)
                    => Start == 0 && End == token.Length ? token : token.Substring(Start, End - Start);
            }
        }

        /// <summary>
        /// Stores GitLab rules grouped into independent sections.
        /// </summary>
        /// <remarks>
        /// See <see href="https://docs.gitlab.com/user/project/codeowners/reference/#sections">GitLab CODEOWNERS section rules</see>.
        /// </remarks>
        private sealed class GitLabRuleSet : RuleSet
        {
            private readonly GitLabSection[] _sections;

            private GitLabRuleSet(GitLabSection[] sections)
            {
                _sections = sections;
            }

            /// <summary>
            /// Parses GitLab sections and rules and counts invalid input.
            /// </summary>
            public static GitLabRuleSet Parse(IEnumerable<string> lines, out int parsingDiagnosticsCount)
            {
                parsingDiagnosticsCount = 0;
                var sections = new List<GitLabSection>();
                // Rules before the first header belong to the unnamed section.
                var currentSection = GitLabSection.CreateUnnamed();
                var activeDefaultOwners = currentSection.DefaultOwners;
                var namedSections = new Dictionary<string, GitLabSection>(StringComparer.OrdinalIgnoreCase);
                sections.Add(currentSection);

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

                        activeDefaultOwners = newSection.DefaultOwners;
                        if (namedSections.TryGetValue(newSection.Name, out var existingSection))
                        {
                            // Repeated section names add rules to the first section with that name.
                            currentSection = existingSection;
                        }
                        else
                        {
                            currentSection = newSection;
                            sections.Add(currentSection);
                            namedSections.Add(currentSection.Name, currentSection);
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

                    var rule = Rule.ParseGitLab(raw, activeDefaultOwners, out var ruleHasDiagnostics);
                    if (rule is null)
                    {
                        parsingDiagnosticsCount++;
                    }
                    else
                    {
                        if (ruleHasDiagnostics)
                        {
                            parsingDiagnosticsCount++;
                        }

                        currentSection.AddRule(rule);
                    }
                }

                // Finish each section after all repeated definitions have been joined.
                foreach (var section in sections)
                {
                    section.FinishParsing();
                }

                return new GitLabRuleSet(sections.ToArray());
            }

            /// <summary>
            /// Matches each section separately and combines the owners from every matching section.
            /// </summary>
            public override string[] Match(string path)
            {
                string[]? singleSectionOwners = null;
                HashSet<string>? combinedOwners = null;
                foreach (var section in _sections)
                {
                    if (!section.TryMatch(path, out var sectionOwners))
                    {
                        continue;
                    }

                    if (singleSectionOwners is null)
                    {
                        singleSectionOwners = sectionOwners;
                        continue;
                    }

                    // Most files match a single section. Allocate a set only when owners from
                    // multiple sections must be combined and de-duplicated.
                    combinedOwners ??= new HashSet<string>(singleSectionOwners, StringComparer.Ordinal);
                    foreach (var owner in sectionOwners)
                    {
                        combinedOwners.Add(owner);
                    }
                }

                if (combinedOwners is null)
                {
                    return singleSectionOwners ?? [];
                }

                var owners = new string[combinedOwners.Count];
                combinedOwners.CopyTo(owners);
                return owners;
            }
        }

        private sealed class GitLabSection
        {
            private List<Rule>? _rulesDuringParsing = new();
            private Rule[] _matchingRules = [];

            /// <summary>
            /// Initializes a new instance of the <see cref="GitLabSection"/> class.
            /// </summary>
            public GitLabSection(string name, string[] defaultOwners)
            {
                Name = name;
                DefaultOwners = defaultOwners.Length == 0 ? [] : defaultOwners;
            }

            public string Name { get; private set; }

            public string[] DefaultOwners { get; private set; }

            /// <summary>
            /// Creates the section used for rules before the first named header.
            /// </summary>
            public static GitLabSection CreateUnnamed() => new(string.Empty, []);

            /// <summary>
            /// Adds a parsed rule to this section.
            /// </summary>
            public void AddRule(Rule rule)
            {
                if (_rulesDuringParsing is null)
                {
                    throw new InvalidOperationException("Rules cannot be added after parsing has finished.");
                }

                _rulesDuringParsing.Add(rule);
            }

            /// <summary>
            /// Prepares the section for matching by keeping the last rule for each exact pattern.
            /// </summary>
            public void FinishParsing()
            {
                var parsedRules = _rulesDuringParsing;
                if (parsedRules is null)
                {
                    throw new InvalidOperationException("The section has already finished parsing.");
                }

                var seenPatterns = new HashSet<string>(StringComparer.Ordinal);
                var matchingRules = new List<Rule>(parsedRules.Count);
                // Walk backwards because later rules replace earlier rules with the same pattern.
                for (var i = parsedRules.Count - 1; i >= 0; i--)
                {
                    var rule = parsedRules[i];
                    if (seenPatterns.Add(rule.GetPatternKeyAndRelease()))
                    {
                        matchingRules.Add(rule);
                    }
                }

                _matchingRules = matchingRules.ToArray();

                // Names, defaults, pattern keys, and the original rule list are only needed while parsing.
                _rulesDuringParsing = null;
                Name = string.Empty;
                DefaultOwners = [];
            }

            /// <summary>
            /// Returns the owners selected by this section, unless a matching exclusion removes the path.
            /// </summary>
            /// <remarks>
            /// See <see href="https://docs.gitlab.com/user/project/codeowners/reference/#exclusion-patterns">GitLab CODEOWNERS exclusion rules</see>.
            /// </remarks>
            public bool TryMatch(string path, [NotNullWhen(true)] out string[]? owners)
            {
                string[]? matchedOwners = null;

                foreach (var rule in _matchingRules)
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

        private sealed partial class Rule
        {
            /// <summary>
            /// Parses one GitLab rule, applies section defaults, and compiles its path pattern.
            /// </summary>
            public static Rule? ParseGitLab(string raw, string[] defaultOwners, out bool hasDiagnostics)
            {
                hasDiagnostics = false;
                SplitRule(raw, out var patternToken, out var ownersSegment, out var hasExplicitOwners);

                var isExclusion = patternToken.StartsWith("!");
                if (isExclusion)
                {
                    patternToken = patternToken.Substring(1, patternToken.Length - 1);
                }

                if (patternToken.Length == 0)
                {
                    return null;
                }

                string[] owners;
                if (isExclusion)
                {
                    owners = [];
                }
                else
                {
                    owners = GitLabOwnerTokenizer.TokenizeGitLab(ownersSegment, out var allOwnersValid);
                    hasDiagnostics = !allOwnersValid;
                }

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

                return new Rule(glob, NormalizeGitLabPatternKey(patternToken), isExclusion, owners);
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
                    var unescape = (i == 0 && patternToken[i] == '\\' && patternToken[i + 1] == '#') ||
                                   (patternToken[i] == '\\' && char.IsWhiteSpace(patternToken[i + 1]));
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
