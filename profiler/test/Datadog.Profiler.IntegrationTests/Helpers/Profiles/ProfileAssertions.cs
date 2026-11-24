// <copyright file="ProfileAssertions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal static class ProfileAssertions
    {
        public static StackTraceAssertions Should(this StackTrace stack) => new StackTraceAssertions(stack);

        public static PprofProfileAssertions Should(this PprofProfile profile) => new PprofProfileAssertions(profile);

        public static ProfileOutputAssertions Should(this ProfileOutput output) => new ProfileOutputAssertions(output);

        public static ProfileLabelsAssertions Should(this ProfileLabels labels) => new ProfileLabelsAssertions(labels);

        private static void Check(bool condition, string message, string because, object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(condition)
                .FailWith("{0}{reason}", message ?? string.Empty);
        }

        private static string Join(IReadOnlyList<ProfileValue> values)
        {
            var parts = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                parts[i] = values[i].ToString();
            }

            return string.Join(", ", parts);
        }

        private static string Join(ProfileValue[] values)
        {
            var parts = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString();
            }

            return string.Join(", ", parts);
        }

        internal sealed class StackTraceAssertions : ReferenceTypeAssertions<StackTrace, StackTraceAssertions>
        {
            public StackTraceAssertions(StackTrace subject)
                : base(subject)
            {
            }

            protected override string Identifier => "stack trace";

            public AndConstraint<StackTraceAssertions> StartWith(StackTrace expected, string because = "", params object[] becauseArgs)
            {
                string failure = null;
                var matched = false;
                if (Subject is null)
                {
                    failure = "Expected a stack trace but found null.";
                }
                else if (expected is null)
                {
                    failure = "Expected the comparison stack to be non-null.";
                }
                else
                {
                    matched = Subject.StartsWith(expected, out failure);
                }

                Check(matched, failure, because, becauseArgs);
                return new AndConstraint<StackTraceAssertions>(this);
            }

            public AndConstraint<StackTraceAssertions> ContainSequence(StackTrace expected, string because = "", params object[] becauseArgs)
            {
                string failure = null;
                var matched = false;
                if (Subject is null)
                {
                    failure = "Expected a stack trace but found null.";
                }
                else if (expected is null)
                {
                    failure = "Expected the comparison stack to be non-null.";
                }
                else
                {
                    matched = Subject.ContainsSequence(expected, out failure);
                }

                Check(matched, failure, because, becauseArgs);
                return new AndConstraint<StackTraceAssertions>(this);
            }
        }

        internal sealed class PprofProfileAssertions : ReferenceTypeAssertions<PprofProfile, PprofProfileAssertions>
        {
            public PprofProfileAssertions(PprofProfile subject)
                : base(subject)
            {
            }

            protected override string Identifier => "pprof profile";

            public AndConstraint<PprofProfileAssertions> HaveSampleTypes(params ProfileValue[] expected)
            {
                var actual = Subject is null ? new List<ProfileValue>() : Subject.SampleTypes;
                var same = Subject is not null && expected is not null && actual.Count == expected.Length;
                if (same)
                {
                    for (var i = 0; i < expected.Length; i++)
                    {
                        if (!actual[i].Equals(expected[i]))
                        {
                            same = false;
                            break;
                        }
                    }
                }

                string message;
                if (Subject is null)
                {
                    message = "Expected a profile but found null.";
                }
                else if (expected is null)
                {
                    message = "Expected the sample type list to be non-null.";
                }
                else
                {
                    message = "Expected profile '" + Subject.Path + "' to have sample types [" + Join(expected) + "] but found [" + Join(actual) + "].";
                }

                Check(same, message, string.Empty, Array.Empty<object>());
                return new AndConstraint<PprofProfileAssertions>(this);
            }
        }

        internal sealed class ProfileOutputAssertions : ReferenceTypeAssertions<ProfileOutput, ProfileOutputAssertions>
        {
            public ProfileOutputAssertions(ProfileOutput subject)
                : base(subject)
            {
            }

            protected override string Identifier => "profile output";

            /// <summary>
            /// Checks the sample types of every profile. Passes when there is no profile.
            /// </summary>
            public AndConstraint<ProfileOutputAssertions> HaveSampleTypes(params ProfileValue[] expected)
            {
                Check(Subject is not null, "Expected a profile output but found null.", string.Empty, Array.Empty<object>());
                if (Subject is not null)
                {
                    foreach (var profile in Subject.Profiles)
                    {
                        profile.Should().HaveSampleTypes(expected);
                    }
                }

                return new AndConstraint<ProfileOutputAssertions>(this);
            }
        }

        internal sealed class ProfileLabelsAssertions : ReferenceTypeAssertions<ProfileLabels, ProfileLabelsAssertions>
        {
            public ProfileLabelsAssertions(ProfileLabels subject)
                : base(subject)
            {
            }

            protected override string Identifier => "profile labels";

            public AndConstraint<ProfileLabelsAssertions> HaveLabel(string name, string value, string because = "", params object[] becauseArgs)
            {
                var found = false;
                if (Subject is not null && name is not null)
                {
                    foreach (var candidate in Subject.GetValues(name))
                    {
                        if (string.Equals(candidate, value, StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                string message;
                if (Subject is null)
                {
                    message = "Expected labels but found null.";
                }
                else
                {
                    message = "Expected label '" + name + "' = '" + value + "' but labels were: " + Subject + ".";
                }

                Check(found, message, because, becauseArgs);
                return new AndConstraint<ProfileLabelsAssertions>(this);
            }
        }
    }
}
