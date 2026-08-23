// <copyright file="CodeOwners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
        // Upper bound for any single glob evaluation: protects the process from pathological
        // patterns in huge CODEOWNERS files. Timed-out rules are treated as non-matching.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);

        private readonly List<Section> _sections;
        private readonly Platform _platform;

        public CodeOwners(string filePath, Platform platform)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            _platform = platform;
            _sections = Parse(File.ReadLines(filePath), platform);
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
                        return DeduplicateOwners(sectionOwners);
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

        private static IEnumerable<string> DeduplicateOwners(string[] owners)
        {
            // Owner lists are tiny (typically a single entry), so scan for duplicates first and skip
            // the HashSet allocation entirely in the common case.
            for (var i = 0; i < owners.Length; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    if (!string.Equals(owners[i], owners[j], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var unique = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var owner in owners)
                    {
                        unique.Add(owner);
                    }

                    return unique;
                }
            }

            return owners;
        }

        private static List<Section> Parse(IEnumerable<string> lines, Platform platform)
        {
            var sections = new List<Section>();
            var current = Section.CreateUnnamed();
            sections.Add(current);

            var lineNo = 0;
            foreach (var line in lines)
            {
                lineNo++;
                var raw = line.Trim();
                if (raw.Length == 0)
                {
                    continue;
                }

                if (TryParseSectionHeader(raw, out var newSection))
                {
                    current = newSection;
                    sections.Add(current);
                    continue;
                }

                if (raw[0] == '#')
                {
                    // Comment line. GitLab parses owners found inside comments so they appear in MR widget,
                    // but those owners are not bound to any path pattern, so we ignore them for matching.
                    continue;
                }

                var entry = Entry.Parse(raw, platform, lineNo);
                if (entry is not null)
                {
                    current.Add(entry);
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

        private static bool TryParseSectionHeader(string raw, [NotNullWhen(true)] out Section? section)
        {
            // Accepted forms:
            //   [Docs]
            //   ^[Go]
            //   [Backend][2] @team @another
            var m = Regex.Match(raw, @"^\s*(\^)?\[(?<name>[^\]]+)\](?:\[(?<cnt>\d+)\])?(?<rest>.*)$");
            if (!m.Success)
            {
                section = null;
                return false;
            }

            var required = !m.Groups[1].Success; // ^ prefix => optional section
            var name = m.Groups["name"].Value.Trim();
            var approvals = 0;
            if (m.Groups["cnt"].Success && int.TryParse(m.Groups["cnt"].Value, out var val))
            {
                approvals = val;
            }

            var defaults = OwnerTokenizer.Tokenize(m.Groups["rest"].Value).ToArray();
            section = new Section(name, required, approvals, defaults);
            return true;
        }

        /// <summary>
        /// Converts a CODEOWNERS ‑style glob into a Regex.
        /// Supports **, *, ?, /‑rooted, and trailing / semantics.
        /// When <paramref name="includeDescendants"/> is set, a direct match also owns every
        /// descendant path in a single evaluation instead of testing each ancestor separately.
        /// </summary>
        private static Regex CompileGlob(string pattern, bool includeDescendants)
        {
            // Escape regex metachars first.
            var rx = Regex.Escape(pattern);

            // Temporary sentinel for ** that we restore after dealing with single *.
            rx = rx.Replace("\\*\\*", "§§DOUBLESTAR§§");
            rx = rx.Replace("\\*", "[^/]*"); // single‑level wildcard
            // A slash right after ** means it can match zero or more intermediate directories:
            // `a/**/b` must also match `a/b`.
            rx = rx.Replace("§§DOUBLESTAR§§/", "(?:.*/)?");
            rx = rx.Replace("§§DOUBLESTAR§§", ".*"); // multi‑level wildcard
            rx = rx.Replace("\\?", "."); // single char

            if (pattern.EndsWith("/"))
            {
                rx += ".*"; // directory pattern matches everything underneath
            }

            if (pattern.StartsWith("/"))
            {
                // keep the escaped leading slash so paths like "/foo/bar" match
                rx = "^" + rx;
            }
            else
            {
                // Allowed anywhere in repo tree; use non‑capturing look‑behind to avoid double counting.
                rx = "(^|.*/)" + rx;
            }

            rx += includeDescendants ? "(?:/.*)?$" : "$";
            return new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        }

#pragma warning disable SA1201
        public enum Platform
#pragma warning restore SA1201
        {
            GitHub,
            GitLab
        }

        private static class OwnerTokenizer
        {
            public static IEnumerable<string> Tokenize(string segment)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    yield break;
                }

                foreach (var token in segment.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token.Contains('@'))
                    {
                        yield return token;
                    }
                }
            }
        }

        private sealed class Section
        {
            private readonly List<Entry> _entries = new();
            private Entry[]? _cache;

            public Section(string name, bool required, int approvalCount, string[] defaultOwners)
            {
                Name = name;
                Required = required;
                ApprovalCount = approvalCount;
                DefaultOwners = defaultOwners.Length == 0 ? [] : defaultOwners;
            }

            public string Name { get; }

            public bool Required { get; }

            public int ApprovalCount { get; }

            public string[] DefaultOwners { get; }

            public static Section CreateUnnamed() => new(string.Empty, required: true, approvalCount: 0, defaultOwners: []);

            public void Add(Entry entry) => _entries.Add(entry);

            public void Seal() => _cache = _entries.AsEnumerable().Reverse().ToArray();

            /// <summary>
            /// GitHub evaluation: exclusion rules are unsupported and ignored, section default owners don't
            /// exist, and the caller stops at the first (i.e. last in file order) matching rule.
            /// </summary>
            public bool TryMatchGitHub(string path, [NotNullWhen(true)] out string[]? owners)
            {
                var rules = _cache ?? [];

                foreach (var rule in rules)
                {
                    // GitHub doesn't support exclusion rules. Keep them parse‑able but ignore when evaluating.
                    if (rule.IsExclusion || !rule.Match(path))
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

                    matchedOwners = rule.Owners.Length > 0 ? rule.Owners : DefaultOwners;
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
            private readonly Regex _regex;
            private readonly string _patternToken;
            private readonly bool _isDirectoryPattern;
            private Regex? _descendantsRegex;

            private Entry(Regex regex, string patternToken, bool exclusion, string[] owners, bool isDirectoryPattern)
            {
                _regex = regex;
                _patternToken = patternToken;
                IsExclusion = exclusion;
                Owners = owners;
                _isDirectoryPattern = isDirectoryPattern;
            }

            public bool IsExclusion { get; }

            public string[] Owners { get; }

            public static Entry? Parse(string raw, Platform platform, int lineNo)
            {
                // Strip inline comments for GitHub. GitLab treats everything after # as data (inline comments unsupported).
                var idxHash = raw.IndexOf('#');
                var effective = idxHash >= 0 && platform == Platform.GitHub ? raw.Substring(0, idxHash).TrimEnd() : raw;
                if (string.IsNullOrWhiteSpace(effective))
                {
                    return null;
                }

                // 2. Tokenise
                //    * GitHub:   simple whitespace split
                //    * GitLab:   split on whitespace NOT escaped with back-slash
                string[] tokens;

                if (platform == Platform.GitLab)
                {
                    // Split on space / tab that are **not** escaped:  (?<!\\)[ \t]+
                    tokens = Regex.Split(effective, @"(?<!\\)[ \t]+", RegexOptions.None, TimeSpan.FromMilliseconds(100))
                                  .Where(t => t.Length > 0)
                                   // Undo the escaping:  "\ " → " ",  "\#" → "#",  "\\" → "\"
                                  .Select(t => Regex.Replace(t, @"\\([ #\\])", "$1"))
                                  .ToArray();
                }
                else
                {
                    tokens = effective.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                }

                if (tokens.Length == 0)
                {
                    return null;
                }

                // 3. Pattern & exclusion
                var patternToken = tokens[0];
                var isExclusion = platform == Platform.GitLab && patternToken.StartsWith("!");
                if (isExclusion)
                {
                    patternToken = patternToken.Substring(1, patternToken.Length - 1);
                }

                // 4. Owners (validate through OwnerTokenizer to drop any bogus tokens)
                var ownersSegment = tokens.Length > 1 ? string.Join(" ", tokens.Skip(1)) : string.Empty;
                var owners = OwnerTokenizer.Tokenize(ownersSegment).ToArray();

                // GitHub owns the contents of directories matched by wildcard-free patterns (e.g.
                // `**/logs`); GitLab requires an explicit trailing slash for directory ownership.
                var isDirectoryPattern = platform == Platform.GitHub && IsDirectoryPattern(patternToken);

                // 5. Compile the glob
                var rx = CompileGlob(patternToken, includeDescendants: false);
                return new Entry(rx, patternToken, isExclusion, owners, isDirectoryPattern);
            }

            private static bool IsDirectoryPattern(string patternToken)
            {
                var lastSegmentStart = patternToken.LastIndexOf('/');
                var lastSegment = lastSegmentStart >= 0 ? patternToken.Substring(lastSegmentStart + 1) : patternToken;
                return lastSegment.Length > 0 &&
                       lastSegment.IndexOf('*') < 0 &&
                       lastSegment.IndexOf('?') < 0;
            }

            private static bool IsMatch(Regex regex, string input)
            {
                try
                {
                    return regex.IsMatch(input);
                }
                catch (RegexMatchTimeoutException)
                {
                    // A pathological pattern must never hang the process: treat it as non-matching.
                    return false;
                }
            }

            public bool Match(string path)
            {
                if (IsMatch(_regex, path))
                {
                    return true;
                }

                // Patterns whose last segment is wildcard-free also own everything inside a matched
                // directory (e.g. `**/logs` owns `/build/logs/error.txt`), while wildcard segments like
                // `docs/*` match individual entries only. The descendant variant of the glob accepts any
                // path below a direct match in a single evaluation.
                return _isDirectoryPattern && IsMatch(LazyGetDescendantsRegex(), path);
            }

            private Regex LazyGetDescendantsRegex()
            {
                // Compiled lazily because most rules never need the descendant variant. The race of two
                // threads compiling simultaneously is benign: both produce identical regexes.
                return _descendantsRegex ??= CompileGlob(_patternToken, includeDescendants: true);
            }
        }
    }
}
