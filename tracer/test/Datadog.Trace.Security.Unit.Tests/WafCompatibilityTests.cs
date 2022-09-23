// <copyright file="WafCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection("WafTests")]
    public class WafCompatibilityTests
    {
        [SkippableFact]
        public void ShouldNotInitialize()
        {
            using var waf = Waf.Create(string.Empty, string.Empty, string.Empty, "1.4.0");
            waf.InitializedSuccessfully.Should().BeFalse();
            waf.InitializationResult.ExportErrors.Should().BeTrue();
        }
    }
}
