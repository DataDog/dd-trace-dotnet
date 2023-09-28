// <copyright file="AspNetCore2ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity
{
    public class AspNetCore2ApiSecurityEnabled : AspNetCoreApiSecurity
    {
        public AspNetCore2ApiSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, true, "AspNetCore2")
        {
        }
    }

    public class AspNetCore2ApiSecurityDisabled : AspNetCoreApiSecurity
    {
        public AspNetCore2ApiSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, false, sampleName: "AspNetCore2")
        {
        }
    }
}
#endif
