// <copyright file="GlobalEnvVariableHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.FleetInstaller;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

[TracerValuesRestorer]
public class GlobalEnvVariableHelperTests(ITestOutputHelper output) : FleetInstallerTestsBase(output)
{
    [SkippableFact]
    public void FullInstallAndUninstall_WhenNoVariablesSet_SetsVariablesAndRemovesThem()
    {
        Skip.IfNot(IsRunningAsAdministrator);

        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);

        AssertAllKeysAreEmpty(values);

        GlobalEnvVariableHelper.SetMachineEnvironmentVariables(Log, values, out var previousValues);
        previousValues.Should().BeEmpty();

        AssertAllEnvVarValuesAreSetAtMachineLevel(values);

        // Confirm that we can clear them all again
        GlobalEnvVariableHelper.RemoveMachineEnvironmentVariables(Log);
        AssertAllKeysAreEmpty(values);
    }

    [SkippableFact]
    public void FullInstallAndUninstall_WhenExistingVariablesPresent_OverwritesVariablesAndRemovesThem()
    {
        Skip.IfNot(IsRunningAsAdministrator);
        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);

        // Add some dummy values
        var expectedPrevious = values.GlobalRequiredEnvVariables
                                     .ToDictionary(x => x.Key, x => x.Key);
        foreach (var kvp in expectedPrevious)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Machine);
        }

        GlobalEnvVariableHelper.SetMachineEnvironmentVariables(Log, values, out var previousValues);
        previousValues.Should().BeEquivalentTo(expectedPrevious);

        AssertAllEnvVarValuesAreSetAtMachineLevel(values);

        // Confirm that we can clear them all again
        GlobalEnvVariableHelper.RemoveMachineEnvironmentVariables(Log);
        AssertAllKeysAreEmpty(values);
    }

    [SkippableFact]
    public void FullInstallAndRevert_WhenExistingVariablesPresent_RestoresPreviousValues()
    {
        Skip.IfNot(IsRunningAsAdministrator);
        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);

        // Add some dummy values
        var expectedPrevious = values.GlobalRequiredEnvVariables
                                     .ToDictionary(x => x.Key, x => x.Key);
        foreach (var kvp in expectedPrevious)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Machine);
        }

        GlobalEnvVariableHelper.SetMachineEnvironmentVariables(Log, values, out var previousValues);
        previousValues.Should().BeEquivalentTo(expectedPrevious);

        AssertAllEnvVarValuesAreSetAtMachineLevel(values);

        // Confirm that we can revert to the previous
        GlobalEnvVariableHelper.RevertMachineEnvironmentVariables(Log, previousValues);
        AssertAllEnvVarValuesAreSetAtMachineLevel(expectedPrevious);
    }

    private static void AssertAllEnvVarValuesAreSetAtMachineLevel(TracerValues values)
        => AssertAllEnvVarValuesAreSetAtMachineLevel(values.GlobalRequiredEnvVariables);

    private static void AssertAllEnvVarValuesAreSetAtMachineLevel(IDictionary<string, string> expected)
    {
        foreach (var kvp in expected)
        {
            Environment.GetEnvironmentVariable(kvp.Key, EnvironmentVariableTarget.Machine).Should().Be(kvp.Value);
        }
    }

    private static void AssertAllKeysAreEmpty(TracerValues values)
    {
        foreach (var kvp in values.GlobalRequiredEnvVariables)
        {
            Environment.GetEnvironmentVariable(kvp.Key, EnvironmentVariableTarget.Machine).Should().BeNullOrEmpty();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class TracerValuesRestorerAttribute : EnvironmentRestorerAttribute
    {
        public TracerValuesRestorerAttribute()
            : base([..new TracerValues(string.Empty).GlobalRequiredEnvVariables.Keys
                                                    .Concat(new TracerValues(string.Empty).IisRequiredEnvVariables.Keys)
                                                    .Distinct()])
        {
        }

        public override EnvironmentVariableTarget Target => EnvironmentVariableTarget.Machine;
    }
}
#endif
