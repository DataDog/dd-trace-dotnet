// <copyright file="WafCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection(nameof(SecuritySequentialTests))]
    public class WafCompatibilityTests
    {
        [SkippableTheory]
        [InlineData("1.3.0")]
        [InlineData("1.10.0")]
        public void ShouldNotInitializeWithExportsMissing(string version)
        {
            var libraryInitializationResult = WafLibraryInvoker.Initialize(version);
            libraryInitializationResult.ExportErrorHappened.Should().BeTrue();
            libraryInitializationResult.Success.Should().BeFalse();
            libraryInitializationResult.WafLibraryInvoker.Should().BeNull();
        }

        [SkippableTheory]
        [InlineData("1.16.0")]
        [InlineData("1.14.0")]
        public void ShouldNotInitializeWithKnownIncompatibility(string version)
        {
            var libraryInitializationResult = WafLibraryInvoker.Initialize(version);
            libraryInitializationResult.VersionNotCompatible.Should().BeTrue();
            libraryInitializationResult.Success.Should().BeFalse();
            libraryInitializationResult.WafLibraryInvoker.Should().BeNull();
        }
    }
}
