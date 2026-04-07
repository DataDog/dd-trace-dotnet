// <copyright file="RegistryHelperTests.cs" company="Datadog">
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

public class RegistryHelperTests(ITestOutputHelper output) : FleetInstallerTestsBase(output)
{
    private readonly List<string> _keysToDelete = [];

    public override void Dispose()
    {
        base.Dispose();
        foreach (var key in _keysToDelete)
        {
            Registry.LocalMachine.DeleteSubKey(key, throwOnMissingSubKey: false);
        }
    }

    [SkippableFact]
    public void AddCrashTrackingKey_AddsKeyIfItDoesntExist()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();

        RegistryHelper.AddCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is there with expected value
        Registry.LocalMachine.OpenSubKey(key)
               ?.GetValue(values.NativeLoaderX64Path)
                .Should()
                .Be(1);
    }

    [SkippableFact]
    public void AddCrashTrackingKey_AddsKeyIfItAlreadyExists()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();
        Registry.LocalMachine.CreateSubKey(key).Should().NotBeNull();

        RegistryHelper.AddCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is there with expected value
        Registry.LocalMachine.OpenSubKey(key)
               ?.GetValue(values.NativeLoaderX64Path)
                .Should()
                .Be(1);
    }

    [SkippableFact]
    public void AddCrashTrackingKey_OverwritesExistingValueIfSet()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();
        var regKey = Registry.LocalMachine.CreateSubKey(key);
        regKey.Should().NotBeNull();
        regKey!.SetValue(values.NativeLoaderX64Path, 0, RegistryValueKind.DWord);

        RegistryHelper.AddCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is there with expected value
        Registry.LocalMachine.OpenSubKey(key)
               ?.GetValue(values.NativeLoaderX64Path)
                .Should()
                .Be(1);
    }

    [SkippableFact]
    public void AddCrashTrackingKey_DoesNotAffectOtherValues()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();
        var otherValueName = "some-other-value";
        var otherValueValue = 0;
        var regKey = Registry.LocalMachine.CreateSubKey(key);
        regKey.Should().NotBeNull();
        regKey!.SetValue(otherValueName, otherValueValue, RegistryValueKind.DWord);

        RegistryHelper.AddCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is there with expected value
        Registry.LocalMachine.OpenSubKey(key)
               ?.GetValue(values.NativeLoaderX64Path)
                .Should()
                .Be(1);

        // verify existing key is untouched
        Registry.LocalMachine.OpenSubKey(key)
               ?.GetValue(otherValueName)
                .Should()
                .Be(otherValueValue);
    }

    [SkippableFact]
    public void RemoveCrashTrackingKey_RemovesValueIfSet()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();
        var keyValue = values.NativeLoaderX64Path;
        Registry.LocalMachine.CreateSubKey(key)
                !.SetValue(keyValue, 0, RegistryValueKind.DWord);

        RegistryHelper.RemoveCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is no longer there
        Registry.LocalMachine.OpenSubKey(key)?.GetValue(keyValue).Should().BeNull();
    }

    [SkippableFact]
    public void RemoveCrashTrackingKey_ReturnsTrueIfKeyDoesntExist()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();
        var keyValue = values.NativeLoaderX64Path;
        Registry.LocalMachine.CreateSubKey(key).Should().NotBeNull();

        RegistryHelper.RemoveCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is no longer there
        Registry.LocalMachine.OpenSubKey(key)?.GetValue(keyValue).Should().BeNull();
    }

    [SkippableFact]
    public void RemoveCrashTrackingKey_ReturnsTrueIfKeyIsNotThere()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key = GetRegistryKey();
        Registry.LocalMachine.OpenSubKey(key).Should().BeNull();

        RegistryHelper.RemoveCrashTrackingKey(Log, values, key)
                      .Should()
                      .BeTrue();

        // verify the key is still not there
        Registry.LocalMachine.OpenSubKey(key).Should().BeNull();
    }

    [SkippableFact]
    public void TryGetIisVersion_ReturnsTrue()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        // Needs to be run on a machine that has IIS installed
        RegistryHelper.TryGetIisVersion(Log, out var version).Should().BeTrue();
    }

    private string GetRegistryKey()
    {
        var key = $@"SOFTWARE\Datadog\Datadog .NET Tracer\{Guid.NewGuid():N}";
        _keysToDelete.Add(key);
        return key;
    }
}

#endif
