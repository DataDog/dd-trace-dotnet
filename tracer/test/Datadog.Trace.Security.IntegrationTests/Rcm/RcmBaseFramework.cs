// <copyright file="RcmBaseFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm;

public class RcmBaseFramework : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public RcmBaseFramework(string sampleName, ITestOutputHelper outputHelper, string shutdownPath, string samplesDir = null, string testName = null)
        : base(sampleName, outputHelper,  shutdownPath, samplesDir, testName)
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "0.5");
    }
}
