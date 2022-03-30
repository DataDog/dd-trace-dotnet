// <copyright file="WafErrorsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafErrorsTests
    {
        [Theory]
        [InlineData(@"{""missing key 'name'"":[""crs-913-110"",""crs-913-120"",""crs-920-260""],""missing key 'tags'"":[""crs-921-110"",""crs-921-140""]}", "erroneous-rule-set.json")]
        public void QueryStringAttack(string errorMessage, string filename)
        {
            Execute(errorMessage, filename);
        }

        private static void Execute(string errormessage, string filename)
        {
            Environment.SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
            using var waf = Waf.Create(filename);
            waf.Should().NotBeNull();
            waf.InitializedSuccessfully.Should().BeTrue();
            waf.InitializationResult.Errors.Should().BeEquivalentTo(errormessage);
        }
    }
}
