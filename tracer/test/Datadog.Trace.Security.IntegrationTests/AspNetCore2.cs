// <copyright file="AspNetCore2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore2TestsSecurityDisabled : AspNetCoreBase
    {
        public AspNetCore2TestsSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore2", fixture, outputHelper, "/shutdown", enableSecurity: false, testName: "AspNetCore2.SecurityDisabled", clearMetaStruct: true)
        {
        }
    }

    public class AspNetCore2TestsSecurityEnabled : AspNetCoreBase
    {
        public AspNetCore2TestsSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore2", fixture, outputHelper, "/shutdown", enableSecurity: true, testName: "AspNetCore2.SecurityEnabled", clearMetaStruct: true)
        {
        }
    }

    public class AspNetCore2TestsSecurityDisabledWithDefaultExternalRulesFile : AspNetCoreSecurityDisabledWithExternalRulesFile
    {
        public AspNetCore2TestsSecurityDisabledWithDefaultExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore2", fixture, outputHelper, "/shutdown", ruleFile: DefaultRuleFile, testName: "AspNetCore2.SecurityDisabled")
        {
        }
    }

    public class AspNetCore2TestsSecurityEnabledWithDefaultExternalRulesFile : AspNetCoreSecurityEnabledWithExternalRulesFile
    {
        public AspNetCore2TestsSecurityEnabledWithDefaultExternalRulesFile(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore2", fixture, outputHelper, "/shutdown", ruleFile: DefaultRuleFile, testName: "AspNetCore2.SecurityEnabled")
        {
        }
    }

    [Collection("IisTests")]
    [Trait("Category", "LinuxUnsupported")]
    public class AspNetCore2TestsSecurityEnabledWithDefaultExternalRulesFileIIS : AspNetCoreSecurityEnabledWithExternalRulesFileIIS
    {
        public AspNetCore2TestsSecurityEnabledWithDefaultExternalRulesFileIIS(IisFixture fixture, ITestOutputHelper outputHelper)
            : base("AspNetCore2", fixture, outputHelper, "/shutdown", IisAppType.AspNetCoreOutOfProcess, ruleFile: AppDomain.CurrentDomain.BaseDirectory + DefaultRuleFile, testName: "AspNetCore2.SecurityEnabled")
        {
        }
    }
}
#endif
