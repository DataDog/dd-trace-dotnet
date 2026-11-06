// <copyright file="ProfilerLogGroupAssertions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using FluentAssertions;
using FluentAssertions.Execution;

namespace Datadog.Profiler.IntegrationTests.Helpers.Logs
{
    internal sealed class ProfilerLogGroupAssertions
    {
        private readonly ProfilerLogGroup _subject;

        public ProfilerLogGroupAssertions(ProfilerLogGroup subject)
        {
            _subject = subject;
        }

        [CustomAssertion]
        public AndConstraint<ProfilerLogGroupAssertions> ContainLine(string expected)
        {
            RequireFiles();
            var matches = _subject.LinesContaining(expected);
            AssertThat(
                matches.Count > 0,
                $"Expected profiler logs to contain a line with \"{expected}\" but no line matched.{_subject.FormatFilesRead()}");

            return new AndConstraint<ProfilerLogGroupAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<ProfilerLogGroupAssertions> NotContainLine(string unexpected)
        {
            RequireFiles();
            var matches = _subject.LinesContaining(unexpected);
            AssertThat(
                matches.Count == 0,
                $"Expected profiler logs not to contain a line with \"{unexpected}\", but found matching lines.{ProfilerLogGroup.FormatMatches(matches)}");

            return new AndConstraint<ProfilerLogGroupAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<ProfilerLogGroupAssertions> NotContainErrors(params string[] allowedErrors)
        {
            RequireFiles();
            var matches = _subject.ErrorLines(allowedErrors);
            AssertThat(
                matches.Count == 0,
                $"Found error lines in profiler logs.{ProfilerLogGroup.FormatMatches(matches)}");

            return new AndConstraint<ProfilerLogGroupAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<ProfilerLogGroupAssertions> ContainFiles()
        {
            RequireFiles();
            return new AndConstraint<ProfilerLogGroupAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<ProfilerLogGroupAssertions> NotContainFiles()
        {
            if (_subject is null)
            {
                AssertThat(false, "Expected profiler logs, but the log group was <null>.");
                return new AndConstraint<ProfilerLogGroupAssertions>(this);
            }

            AssertThat(
                _subject.Files.Count == 0,
                $"Expected no file to match {_subject.Pattern} in {_subject.Directory}, but found:{_subject.FormatFilesRead()}");

            return new AndConstraint<ProfilerLogGroupAssertions>(this);
        }

        private static void AssertThat(bool condition, string message)
        {
            Execute.Assertion
                .ForCondition(condition)
                .FailWith("{0}", message);
        }

        private void RequireFiles()
        {
            if (_subject is null)
            {
                AssertThat(false, "Expected profiler logs, but the log group was <null>.");
                return;
            }

            AssertThat(
                _subject.Files.Count > 0,
                $"no file found for {_subject.Pattern} in {_subject.Directory}");
        }
    }
}
