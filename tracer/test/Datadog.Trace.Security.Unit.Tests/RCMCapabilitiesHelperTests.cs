// <copyright file="RCMCapabilitiesHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Numerics;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.RemoteConfigurationManagement;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class RCMCapabilitiesHelperTests
    {
        public static TheoryData<string, BigInteger, bool> RcmCapabilitiesTestCases()
        => new TheoryData<string, BigInteger, bool>
        {
                    { "2.54", RcmCapabilitiesIndices.AsmRaspSqli, true },
                    { "2.53", RcmCapabilitiesIndices.AsmRaspSqli, false },
                    { "2.51", RcmCapabilitiesIndices.AsmRaspLfi, true },
                    { "2.50", RcmCapabilitiesIndices.AsmRaspLfi, false },
                    { "2.51", RcmCapabilitiesIndices.AsmRaspSsrf, true },
                    { "2.50", RcmCapabilitiesIndices.AsmRaspSsrf, false },
                    { "3.2", RcmCapabilitiesIndices.AsmRaspShi, true },
                    { "3.1", RcmCapabilitiesIndices.AsmRaspShi, false },
                    { "3.2", RcmCapabilitiesIndices.AsmExclusionData, true },
                    { null, RcmCapabilitiesIndices.AsmExclusionData, false },
                    { "INVALID", RcmCapabilitiesIndices.AsmExclusionData, false },
        };

        [Theory]
        [MemberData(nameof(RcmCapabilitiesTestCases))]

        public void GivenAWafVersion_WhenAskedForRCMCapability_ResultIsCorrect(string wafVersion, BigInteger capability, bool result)
        {
            Assert.Equal(result, RCMCapabilitiesHelper.WafSupportsCapability(capability, wafVersion));
        }
    }
}
