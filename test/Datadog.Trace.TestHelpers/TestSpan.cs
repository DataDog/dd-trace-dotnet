// <copyright file="TestSpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class TestSpan : ISpan
    {
        public string ResourceName { get; set; }

        public string Type { get; set; }

        public bool Error { get; set; }

        private Dictionary<string, string> Tags { get; } = new Dictionary<string, string>();

        ISpan ISpan.SetTag(string key, string value)
        {
            SetTagInternal(key, value);

            return this;
        }

        public string GetTag(string key)
            => Tags.TryGetValue(key, out var tagValue)
                   ? tagValue
                   : null;

        public void SetException(Exception exception)
        {
            Error = true;

            SetTagInternal(Trace.Tags.ErrorMsg, exception.Message);
            SetTagInternal(Trace.Tags.ErrorStack, exception.StackTrace);
            SetTagInternal(Trace.Tags.ErrorType, exception.GetType().ToString());
        }

        private void SetTagInternal(string key, string value)
        {
            if (value == null)
            {
                Tags.Remove(key);
            }
            else
            {
                Tags[key] = value;
            }
        }
    }
}
