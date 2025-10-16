// <copyright file="AppHostHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using Datadog.FleetInstaller;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

public class AppHostHelperTests(ITestOutputHelper output) : FleetInstallerTestsBase(output)
{
    [SkippableFact]
    public void AreIisManagementToolsAvailable_ReturnsTrue()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        // Needs to be run on a machine that has IIS installed with management tools available
        AppHostHelper.AreIisManagementToolsAvailable(Log).Should().BeTrue();
    }
}
#endif
