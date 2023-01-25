// <copyright file="WafCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
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
            var initializationResult = WafLibraryInvoker.Initialize("1.4.0");
            initializationResult.Success.Should().BeFalse();
            initializationResult.ExportErrorHappened.Should().BeTrue();
        }
    }
}
