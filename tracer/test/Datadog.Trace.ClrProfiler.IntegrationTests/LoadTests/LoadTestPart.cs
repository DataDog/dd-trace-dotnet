// <copyright file="LoadTestPart.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LoadTests
{
    public class LoadTestPart
    {
        public string Application { get; set; }

        public int? Port { get; set; }

        public bool IsAnchor { get; set; }

        public bool TimeToSetSail { get; set; }

        public string[] CommandLineArgs { get; set; }

        public EnvironmentHelper EnvironmentHelper { get; set; }

        public MockTracerAgent Agent { get; set; }

        public Process Process { get; set; }

        public ProcessResult ProcessResult { get; set; }
    }
}
