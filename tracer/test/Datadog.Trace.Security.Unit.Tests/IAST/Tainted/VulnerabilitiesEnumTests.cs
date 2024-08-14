// <copyright file="VulnerabilitiesEnumTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Iast;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST.Tainted;

public class VulnerabilitiesEnumTests
{
#pragma warning disable SA1401 // Fields should be private
    public static List<object[]> TestData = GetTestData();
#pragma warning restore SA1401 // Fields should be private

    [Theory]
    [MemberData(nameof(TestData))]
    public void AllVulnerabilitiesCreatedExistsInTheSchema(object vulnerability, string[] vulnerabilitiesNames)
    {
        // All types of vulnerabilities created in the tracer must exist in the Vulnerabilities enum of the Vulnerability Schema
        // https://github.com/DataDog/experimental/blob/main/teams/asm/vulnerability_schema/vulnerability_schema.json
        // The file "vulnerability_schema.json" must stay updated as new vulnerabilities are added

        var vulnerabilityType = (VulnerabilityType)vulnerability;
        var vulnerabilityTypeName = VulnerabilityTypeName.GetName(vulnerabilityType);

        Assert.Contains(vulnerabilityTypeName, vulnerabilitiesNames);
    }

    private static List<object[]> GetTestData()
    {
        // The test data consist of:
        // - The vulnerability type from the enum
        // - Array of the vulnerability names, retrieved from the json schema

        var jsonContent = ResourceHelper.ReadAllText<VulnerabilitiesEnumTests>("vulnerability_schema.json");
        var jsonSchema = JObject.Parse(jsonContent);

        var vulnerabilityTypes = (JArray)jsonSchema.SelectToken("definitions.VulnerabilityType.enum");
        if (vulnerabilityTypes is null)
        {
            throw new Exception("The vulnerability_schema.json file is not valid");
        }

        var testData = new List<object[]>();
        var arrayEnumsNameFromJson = vulnerabilityTypes.Select(x => x.ToString()).ToArray();

        foreach (var vulnerabilityType in Enum.GetValues(typeof(VulnerabilityType)))
        {
            // Exclude None
            if ((VulnerabilityType)vulnerabilityType == VulnerabilityType.None)
            {
                continue;
            }

            testData.Add([vulnerabilityType, arrayEnumsNameFromJson]);
        }

        return testData;
    }
}
