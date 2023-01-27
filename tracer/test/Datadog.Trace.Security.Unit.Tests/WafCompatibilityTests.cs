// <copyright file="WafCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Waf.NativeBindings;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafCompatibilityTests
    {
        [SkippableFact]
        public void ShouldNotInitialize()
        {
            var libraryInitializationResult = WafLibraryInvoker.Initialize("1.4.0");
            libraryInitializationResult.Success.Should().BeFalse();
            libraryInitializationResult.ExportErrorHappened.Should().BeTrue();
            libraryInitializationResult.WafLibraryInvoker.Should().BeNull();
        }
    }
}
