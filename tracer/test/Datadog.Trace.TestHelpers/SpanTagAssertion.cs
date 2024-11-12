// <copyright file="SpanTagAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Tags;

namespace Datadog.Trace.TestHelpers
{
    public class SpanTagAssertion<T> : SpanAssertion
    {
        private readonly Result _result;
        private readonly IDictionary<string, T> _tags;

        internal SpanTagAssertion(Result result, IDictionary<string, T> tags)
        {
            _result = result;
            _tags = tags;
        }

        public static void DefaultTagAssertions(SpanTagAssertion<T> s) => s
            .IsPresent("env")
            .IsOptional("runtime-id") // TODO: Make runtime-id required on all spans, per our span attributes push
            .IsOptional("language")   // TODO: Make language required on all spans, per our span attributes push
            .IsOptional("version")
            .IsOptional("_dd.p.dm")   // "decision maker", but contains the sampling mechanism
            .IsOptional("_dd.p.tid")  // contains the upper 64 bits of a 128-bit trace id
            .IsOptional("_dd.parent_id") // Contains the 16 length hex decoded last found parent_id found set from either exising `p` tag on tracestate or headers
            .IsOptional("error.msg")
            .IsOptional("error.type")
            .IsOptional("error.stack")
            .IsOptional("_dd.git.repository_url")
            .IsOptional("_dd.git.commit.sha");

        public static void DefaultMetricAssertions(SpanTagAssertion<T> s) => s
            .IsOptional("_dd.tracer_kr")
            .IsOptional("_dd.agent_psr")
            .IsOptional("process_id")
            .IsOptional("_sampling_priority_v1")
            .IsOptional("_dd.top_level");

        public static void AssertNoRemainingTags(SpanTagAssertion<T> s)
        {
            foreach (var tag in s._result.ExcludeTags)
            {
                s._tags.Remove(tag);
            }

            if (s._tags.Count > 0)
            {
                s._result.WithFailure(GenerateNoRemainingTagsFailureString(s._tags.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString())));
            }
        }

        public SpanTagAssertion<T> IsPresent(string tagName, params string[] fallbackTagNames) => IsOptionalOrPresent(isRequired: true, new string[] { tagName }.Concat(fallbackTagNames).ToArray());

        public SpanTagAssertion<T> IsOptional(string tagName, params string[] fallbackTagNames) => IsOptionalOrPresent(isRequired: false, new string[] { tagName }.Concat(fallbackTagNames).ToArray());

        public SpanTagAssertion<T> Matches(string tagName, T expectedValue)
        {
            bool keyExists = _tags.TryGetValue(tagName, out T value);
            if (keyExists)
            {
                _tags.Remove(tagName);
            }

            if (value is null || !value.Equals(expectedValue))
            {
                _result.WithFailure(GenerateMatchesFailureString("tag", tagName, expectedValue.ToString(), value?.ToString() ?? "null"));
            }

            return this;
        }

        public SpanTagAssertion<T> MatchesOneOf(string tagName, params T[] expectedValues)
        {
            if (_tags.TryGetValue(tagName, out T value))
            {
                _tags.Remove(tagName);
            }

            if (expectedValues.Where(s => s.Equals(value)).SingleOrDefault() is null)
            {
                string expectedValueString = "["
                                + string.Join(",", expectedValues.Select(s => $"\"{s}\"").ToArray())
                                + "]";

                _result.WithFailure(GenerateMatchesOneOfFailureString("tag", tagName, expectedValueString, value?.ToString()));
            }

            return this;
        }

        public SpanTagAssertion<T> IfPresentMatches(string tagName, T expectedValue)
        {
            bool keyExists = _tags.TryGetValue(tagName, out T value);
            if (keyExists)
            {
                _tags.Remove(tagName);
            }
            else
            {
                return this;
            }

            if (!value.Equals(expectedValue))
            {
                _result.WithFailure(GenerateMatchesFailureString("tag", tagName, expectedValue.ToString(), value.ToString()));
            }

            return this;
        }

        public SpanTagAssertion<T> IfPresentMatchesOneOf(string tagName, params T[] expectedValues)
        {
            if (_tags.TryGetValue(tagName, out T value))
            {
                _tags.Remove(tagName);
            }
            else
            {
                return this;
            }

            if (expectedValues.Where(s => s.Equals(value)).SingleOrDefault() is null)
            {
                string expectedValueString = "["
                                + string.Join(",", expectedValues.Select(s => $"\"{s}\"").ToArray())
                                + "]";

                _result.WithFailure(GenerateMatchesOneOfFailureString("tag", tagName, expectedValueString, value?.ToString()));
            }

            return this;
        }

        private SpanTagAssertion<T> IsOptionalOrPresent(bool isRequired, params string[] tagNames)
        {
            // Instead of passing all arguments using params, require one string argument at compile-time
            List<string> foundTags = new();

            foreach (var tagName in tagNames)
            {
                if (_tags.TryGetValue(tagName, out T value) && value is not null)
                {
                    _tags.Remove(tagName);
                    foundTags.Add(tagName);
                }
            }

            if (isRequired && foundTags.Count == 0)
            {
                var propertyName = string.Join(" or ", tagNames.Select(s => '"' + s + '"'));
                _result.WithFailure(GeneratePresentFailureString("tag", propertyName));
            }
            else if (foundTags.Count > 1)
            {
                var propertyName = string.Join(" and ", tagNames.Select(s => '"' + s + '"'));
                var found = string.Join(" and ", foundTags.Select(s => '"' + s + '"'));
                _result.WithFailure(GeneratePresentFoundMultipleFailureString("tag", propertyName, found));
            }

            return this;
        }
    }
}
