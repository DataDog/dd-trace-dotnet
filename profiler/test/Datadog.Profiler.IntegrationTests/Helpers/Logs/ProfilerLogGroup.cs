// <copyright file="ProfilerLogGroup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    internal sealed class ProfilerLogGroup
    {
        // Same marker the smoke tests used to reject a native log: "| error |" ignoring case.
        private static readonly Regex ErrorLinePattern = new(@"\| error \|", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IReadOnlyList<ProfilerLogFile> _files;

        private ProfilerLogGroup(string directory, string pattern, IReadOnlyList<ProfilerLogFile> files)
        {
            Directory = directory;
            Pattern = pattern;
            _files = files;
        }

        public string Directory { get; }

        public string Pattern { get; }

        public IReadOnlyList<ProfilerLogFile> Files => _files;

        public static ProfilerLogGroup FromFiles(string directory, string fileNameOrGlob, IReadOnlyList<ProfilerLogFile> files)
        {
            if (string.IsNullOrEmpty(fileNameOrGlob))
            {
                throw new ArgumentException("A file name or glob is required.", nameof(fileNameOrGlob));
            }

            var matched = new List<ProfilerLogFile>();
            foreach (var file in files)
            {
                if (NameMatches(file.FileName, fileNameOrGlob))
                {
                    matched.Add(file);
                }
            }

            return new ProfilerLogGroup(directory, fileNameOrGlob, matched);
        }

        public static string FormatMatches(IReadOnlyList<ProfilerLogMatch> matches)
        {
            var builder = new StringBuilder();
            foreach (var match in matches)
            {
                builder.AppendLine();
                builder.Append(match.Path);
                builder.Append(':');
                builder.Append(match.LineNumber);
                builder.Append(": ");
                builder.Append(match.Line);
            }

            return builder.ToString();
        }

        public ProfilerLogGroup Matching(string fileNameOrGlob)
        {
            return FromFiles(Directory, fileNameOrGlob, _files);
        }

        public IReadOnlyList<ProfilerLogMatch> LinesEqualTo(string line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            return Find(candidate => string.Equals(candidate, line, StringComparison.Ordinal));
        }

        public IReadOnlyList<ProfilerLogMatch> LinesContaining(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return Find(candidate => candidate.Contains(text, StringComparison.Ordinal));
        }

        public IReadOnlyList<ProfilerLogMatch> LinesMatching(Regex pattern)
        {
            if (pattern is null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            return Find(pattern.IsMatch);
        }

        public IReadOnlyList<ProfilerLogMatch> ErrorLines(string[] allowedErrors)
        {
            var matches = Find(ErrorLinePattern.IsMatch);
            if (allowedErrors is null || allowedErrors.Length == 0)
            {
                return matches;
            }

            var disallowed = new List<ProfilerLogMatch>();
            foreach (var match in matches)
            {
                if (!IsAllowed(match.Line, allowedErrors))
                {
                    disallowed.Add(match);
                }
            }

            return disallowed;
        }

        public string FormatFilesRead()
        {
            var builder = new StringBuilder();
            builder.AppendLine();
            builder.Append("Files read:");
            foreach (var file in _files)
            {
                builder.AppendLine();
                builder.Append(file.Path);
            }

            return builder.ToString();
        }

        private static bool IsAllowed(string line, string[] allowedErrors)
        {
            foreach (var allowed in allowedErrors)
            {
                if (allowed is not null && line.Contains(allowed, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NameMatches(string fileName, string pattern)
        {
            if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
            {
                return string.Equals(fileName, pattern, StringComparison.Ordinal);
            }

            var regexPattern = new StringBuilder("^");
            foreach (var ch in pattern)
            {
                switch (ch)
                {
                    case '*':
                        regexPattern.Append(".*");
                        break;
                    case '?':
                        regexPattern.Append('.');
                        break;
                    default:
                        regexPattern.Append(Regex.Escape(ch.ToString()));
                        break;
                }
            }

            regexPattern.Append('$');
            return Regex.IsMatch(fileName, regexPattern.ToString(), RegexOptions.CultureInvariant);
        }

        private IReadOnlyList<ProfilerLogMatch> Find(Func<string, bool> predicate)
        {
            var matches = new List<ProfilerLogMatch>();
            foreach (var file in _files)
            {
                for (var i = 0; i < file.Lines.Count; i++)
                {
                    var line = file.Lines[i];
                    if (predicate(line))
                    {
                        matches.Add(new ProfilerLogMatch(file.Path, i + 1, line));
                    }
                }
            }

            return matches;
        }
    }
}
