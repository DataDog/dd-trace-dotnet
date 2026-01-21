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
    /// A feature‑complete, allocation‑conscious CODEOWNERS parser that supports both GitHub and GitLab
    /// semantics (sections, exclusions, optional sections, approval counts, role owners, globstar, etc.).
    /// Usage:
    ///   var owners = new CodeOwners(pathToFile, CodeOwners.Platform.GitLab).Match("src/app/Program.cs");
    ///   // owners is an IEnumerable{string} of unique owners that apply to that file.
    /// </summary>
    internal sealed class CodeOwners
    {
        private readonly IReadOnlyList<Section> _sections;
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
            var owners = new HashSet<string>(StringComparer.Ordinal);
            var normalizedPath = path.IndexOf('\\') >= 0 ? path.Replace('\\', '/') : path;

            foreach (var section in _sections)
            {
                if (section.TryMatch(normalizedPath, _platform, out var sectionOwners))
                {
                    foreach (var o in sectionOwners)
                    {
                        owners.Add(o);
                    }
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
                var raw = line.TrimEnd();
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

            // Last‑rule precedence: iterate rules in reverse order at run‑time without additional copies.
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
        /// </summary>
        private static Regex CompileGlob(string pattern)
        {
            // Escape regex metachars first.
            var rx = Regex.Escape(pattern);

            // Temporary sentinel for ** that we restore after dealing with single *.
            rx = rx.Replace("\\*\\*", "§§DOUBLESTAR§§");
            rx = rx.Replace("\\*", "[^/]*"); // single‑level wildcard
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

            rx += "$";
            return new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                    if (token.StartsWith("@@") || token.StartsWith("@") || token.Contains("@"))
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

            public bool TryMatch(string path, Platform platform, out IEnumerable<string> owners)
            {
                owners = [];
                var rules = _cache ?? [];

                foreach (var rule in rules)
                {
                    // GitHub doesn’t support exclusion rules. Keep them parse‑able but ignore when evaluating.
                    if (rule.IsExclusion && platform == Platform.GitHub)
                    {
                        continue;
                    }

                    if (!rule.Match(path))
                    {
                        continue;
                    }

                    if (rule.IsExclusion)
                    {
                        // Excluded for this section; stop evaluating this section.
                        return false;
                    }

                    owners = rule.Owners.Length > 0 ? rule.Owners : DefaultOwners;
                    return owners.Any();
                }

                if (DefaultOwners.Length > 0)
                {
                    owners = DefaultOwners;
                    return true;
                }

                return false;
            }
        }

        private sealed class Entry
        {
            private readonly Regex _regex;

            private Entry(Regex regex, bool exclusion, string[] owners)
            {
                _regex = regex;
                IsExclusion = exclusion;
                Owners = owners;
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

                // 5. Compile the glob
                var rx = CompileGlob(patternToken);
                return new Entry(rx, isExclusion, owners);
            }

            public bool Match(string path) => _regex.IsMatch(path);
        }
    }
}
