// <copyright file="TestResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    internal class TestResult
    {
        public UnitTestOutcome Outcome { get; set; }

        public Exception? TestFailureException { get; set; }

        public int InnerResultsCount { get; set; }

        public TimeSpan Duration { get; set; }

        public string? DisplayName { get; set; }
    }
}
