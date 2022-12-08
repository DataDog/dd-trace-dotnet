// <copyright file="IisFixtureBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>using System;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyTests;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests;

public class IisFixtureBase : AspNetBase
{
    private readonly IisFixture _iisFixture;

    public IisFixtureBase(string sampleName, IisFixture iisFixture, ITestOutputHelper outputHelper, bool classicMode, string samplesDir = null, string testName = null)
        : base(sampleName, outputHelper, null, samplesDir, testName)
    {
        _iisFixture = iisFixture;
        _iisFixture.ShutdownPath = "/home/shutdown";
        _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        SetHttpPort(iisFixture.HttpPort);
    }
}
