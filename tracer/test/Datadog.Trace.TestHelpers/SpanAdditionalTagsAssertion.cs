// <copyright file="SpanAdditionalTagsAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.TestHelpers
{
    public class SpanAdditionalTagsAssertion : SpanAssertion
    {
        private readonly Result _result;
        private readonly IDictionary<string, string> _tags;

        internal SpanAdditionalTagsAssertion(Result result, IDictionary<string, string> tags)
        {
            _result = result;
            _tags = tags;
        }

        public SpanAdditionalTagsAssertion PassesThroughSource(string description, ISet<string> tagNames)
        {
            foreach (var tag in tagNames)
            {
                _tags.Remove(tag);
            }

            return this;
        }
    }
}
