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
    public void SetAndRemove_WhenNoVariablesSet_SetsVariablesAndRemovesThem()
    {
        Skip.IfNot(IsRunningAsAdministrator);

        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);

        AssertAllKeysAreEmpty(values);

        GlobalEnvVariableHelper.SetMachineEnvironmentVariables(Log, values, out var previousValues);
        previousValues.Should().AllSatisfy(kvp => kvp.Value.Should().BeNull());

        AssertAllEnvVarValuesAreSetAtMachineLevel(values);

        // Confirm that we can clear them all again
        GlobalEnvVariableHelper.RemoveMachineEnvironmentVariables(Log);
        AssertAllKeysAreEmpty(values);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(50)]
    public void SetAndRemove_WhenExistingVariablesPresent_OverwritesVariablesAndRemovesThem(int skip)
    {
        Skip.IfNot(IsRunningAsAdministrator);
        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);

        // Add some dummy values
        var expectedPrevious = SetPreviousEnvVariables(values, skip);

        GlobalEnvVariableHelper.SetMachineEnvironmentVariables(Log, values, out var previousValues);
        previousValues.Should().BeEquivalentTo(expectedPrevious);

        AssertAllEnvVarValuesAreSetAtMachineLevel(values);

        // Confirm that we can clear them all again
        GlobalEnvVariableHelper.RemoveMachineEnvironmentVariables(Log);
        AssertAllKeysAreEmpty(values);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(50)]
    public void SetAndRevert_WhenExistingVariablesPresent_RestoresPreviousValues(int skip)
    {
        Skip.IfNot(IsRunningAsAdministrator);
        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);

        // Add some dummy values
        var expectedPrevious = SetPreviousEnvVariables(values, skip);

        GlobalEnvVariableHelper.SetMachineEnvironmentVariables(Log, values, out var previousValues);
        previousValues.Should().BeEquivalentTo(expectedPrevious);

        AssertAllEnvVarValuesAreSetAtMachineLevel(values);

        // Confirm that we can revert to the previous
        GlobalEnvVariableHelper.RevertMachineEnvironmentVariables(Log, previousValues);
        AssertAllEnvVarValuesAreSetAtMachineLevel(expectedPrevious);
    }

    private static Dictionary<string, string> SetPreviousEnvVariables(TracerValues values, int skip)
    {
        // default to null values initially
        var previousSetValues = values
                               .GlobalRequiredEnvVariables
                               .ToDictionary(x => x.Key, x => (string)null);

        foreach (var kvp in previousSetValues.Skip(skip).ToList())
        {
            previousSetValues[kvp.Key] = kvp.Value;
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Machine);
        }

        return previousSetValues;
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
