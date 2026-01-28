// <copyright file="WafCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

using LoadStatus = Datadog.Trace.AppSec.Waf.Initialization.LibraryInitializationResult.LoadStatus;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection(nameof(SecuritySequentialTests))]
    public class WafCompatibilityTests
    {
        [SkippableTheory]
        [InlineData("1.3.0")]
        [InlineData("1.10.0")]
        [InlineData("1.14.0")]
        [InlineData("1.16.0")]
        [InlineData("1.23.0")]
        public void ShouldNotInitialize(string version)
        {
            var libraryInitializationResult = WafLibraryInvoker.Initialize(null, null, version);
            libraryInitializationResult.Success.Should().BeFalse();
            libraryInitializationResult.WafLibraryInvoker.Should().BeNull();
        }
    }
}
