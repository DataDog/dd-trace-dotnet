// <copyright file="WafCompatibilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafCompatibilityTests
    {
        [SkippableFact]
        public void ShouldNotInitializeWithExportsMissing()
        {
            SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.MacOs, SkipOn.ArchitectureValue.ARM64);
            var libraryInitializationResult = WafLibraryInvoker.Initialize("1.3.0");
            libraryInitializationResult.ExportErrorHappened.Should().BeTrue();
            libraryInitializationResult.Success.Should().BeFalse();
            libraryInitializationResult.WafLibraryInvoker.Should().BeNull();
        }

        // [SkippableFact]
        // public void ShouldNotInitializeWithDiagnosticsMissing()
        // {
        //     SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.MacOs, SkipOn.ArchitectureValue.ARM64);
        //     var libraryInitializationResult = WafLibraryInvoker.Initialize("1.10.0");
        //     libraryInitializationResult.Success.Should().BeTrue();
        //     libraryInitializationResult.WafLibraryInvoker.Should().NotBeNull();
        //     var initResult = Waf.Create(libraryInitializationResult.WafLibraryInvoker, string.Empty, string.Empty);
        //     initResult.Success.Should().BeFalse();
        //     initResult.Errors.Should().ContainKey("diagnostics-error");
        //     initResult.Errors.Should().ContainValue("Waf didn't provide a valid diagnostics object at initialization, most likely due to an older waf version < 1.11.0");
        // }
    }
}
