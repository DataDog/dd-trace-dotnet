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
    public class WafCompatibilityTests
    {
        [SkippableFact]
        public void ShouldNotInitializeWithExportsMissing()
        {
            // for some reason, these tests cause a "DDWAF_ERROR": [ddwaf_run]interface.cpp(206): std::bad_alloc on mac when run with others
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            var libraryInitializationResult = WafLibraryInvoker.Initialize("1.3.0");
            libraryInitializationResult.ExportErrorHappened.Should().BeTrue();
            libraryInitializationResult.Success.Should().BeFalse();
            libraryInitializationResult.WafLibraryInvoker.Should().BeNull();
        }

        [SkippableFact]
        public void ShouldNotInitializeWithDiagnosticsMissing()
        {
            // for some reason, these tests cause a "DDWAF_ERROR": [ddwaf_run]interface.cpp(206): std::bad_alloc on mac when run with others
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            var libraryInitializationResult = WafLibraryInvoker.Initialize("1.10.0");
            libraryInitializationResult.Success.Should().BeTrue();
            libraryInitializationResult.WafLibraryInvoker.Should().NotBeNull();
            var initResult = Waf.Create(libraryInitializationResult.WafLibraryInvoker, string.Empty, string.Empty);
            initResult.Success.Should().BeFalse();
            initResult.IncompatibleWaf.Should().BeTrue();
        }
    }
}
