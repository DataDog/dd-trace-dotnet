// <copyright file="AspNetCore2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1

using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore2 : AspNetCoreBase
    {
        public AspNetCore2(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore2", fixture, outputHelper, "/shutdown")
        {
        }
    }
}
#endif
