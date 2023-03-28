// <copyright file="SpanTagAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Tags;

namespace Datadog.Trace.TestHelpers
{
    public class SpanTagAssertion : SpanAssertion
    {
        private readonly Result _result;
        private readonly IDictionary<string, string> _tags;

        internal SpanTagAssertion(Result result, IDictionary<string, string> tags)
        {
            _result = result;
            _tags = tags;
        }

        public static void DefaultTagAssertions(SpanTagAssertion s) => s
            .IsPresent("env")
            .IsOptional("runtime-id") // TODO: Make runtime-id required on all spans, per our span attributes push
            .IsOptional("language") // TODO: Make language required on all spans, per our span attributes push
            .IsOptional("version")
            .IsOptional("_dd.p.dm")
            .IsOptional("error.msg")
            .IsOptional("error.type")
            .IsOptional("error.stack")
            .IsOptional(Tags.GitRepositoryUrl)
            .IsOptional(Tags.GitCommitSha);

        public static void AssertNoRemainingTags(SpanTagAssertion s)
        {
            foreach (var tag in s._result.ExcludeTags)
            {
                s._tags.Remove(tag);
            }

            if (s._tags.Count > 0)
            {
                s._result.WithFailure(GenerateNoRemainingTagsFailureString(s._tags));
            }
        }

        public SpanTagAssertion IsPresent(string tagName)
        {
            bool keyExists = _tags.TryGetValue(tagName, out string value);
            if (keyExists)
            {
                _tags.Remove(tagName);
            }

            if (!keyExists || value is null)
            {
                _result.WithFailure(GeneratePresentFailureString("tag", tagName));
            }

            return this;
        }

        public SpanTagAssertion IsOptional(string tagName)
        {
            bool keyExists = _tags.TryGetValue(tagName, out string value);
            if (keyExists)
            {
                _tags.Remove(tagName);
            }

            return this;
        }

        public SpanTagAssertion Matches(string tagName, string expectedValue)
        {
            bool keyExists = _tags.TryGetValue(tagName, out string value);
            if (keyExists)
            {
                _tags.Remove(tagName);
            }

            if (value != expectedValue)
            {
                _result.WithFailure(GenerateMatchesFailureString("tag", tagName, expectedValue, value));
            }

            return this;
        }

        public SpanTagAssertion MatchesOneOf(string tagName, params string[] expectedValues)
        {
            bool keyExists = _tags.TryGetValue(tagName, out string value);
            if (keyExists)
            {
                _tags.Remove(tagName);
            }

            if (expectedValues.Where(s => s == value).SingleOrDefault() is null)
            {
                string expectedValueString = "["
                             + string.Join(",", expectedValues.Select(s => $"\"{s}\"").ToArray())
                             + "]";

                _result.WithFailure(GenerateMatchesOneOfFailureString("tag", tagName, expectedValueString, value));
            }

            return this;
        }
    }
}
