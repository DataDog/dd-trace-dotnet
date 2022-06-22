// <copyright file="AspNetMvc5QueryStringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5TestsQueryString : AspNetMvc5QueryStringTests
    {
        public AspNetMvc5TestsQueryString(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsQueryStringDisabled : AspNetMvc5QueryStringTests
    {
        public AspNetMvc5TestsQueryStringDisabled(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, false)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc5QueryStringTests : AspNetMvc5Tests
    {
        private readonly bool _enableQueryStringReporting;

        protected AspNetMvc5QueryStringTests(IisFixture iisFixture, ITestOutputHelper output, bool enableQueryStringReporting)
            : base(iisFixture, output, false, true)
        {
            _enableQueryStringReporting = enableQueryStringReporting;
            SetEnvironmentVariable(ConfigurationKeys.EnableQueryStringReporting, _enableQueryStringReporting.ToString());
        }

        public static new TheoryData<string, int> Data() => new() { { "/?authentic1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2", 200 }, };

        protected override string GetTestName()
            => nameof(AspNetMvc5QueryStringTests)
             + (_enableQueryStringReporting ? ".WithQueryString" : ".WithoutQueryString");
    }
}

#endif
