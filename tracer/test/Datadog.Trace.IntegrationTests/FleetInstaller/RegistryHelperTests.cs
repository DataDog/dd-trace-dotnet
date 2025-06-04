// <copyright file="RegistryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.FleetInstaller;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

public class RegistryHelperTests(ITestOutputHelper output) : FleetInstallerTestsBase(output)
{
    private const string IisRegKeyValueName = "Environment";
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

    [SkippableFact]
    public void SetIisRegistrySettings_AddsKeyIfItDoesntExist()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key1 = GetRegistryKey();
        var key2 = GetRegistryKey();

        RegistryHelper.SetIisRegistrySettings(Log, values, key1, key2)
                      .Should()
                      .BeTrue();

        // verify the key is there with expected value
        var expected = values.IisRequiredEnvVariables.Select(kvp => kvp + "=" + kvp.Value).ToArray();
        expected.Should().HaveSameCount(values.IisRequiredEnvVariables);
        VerifyIisRegKey(key1, expected);
        VerifyIisRegKey(key2, expected);
    }

    [SkippableFact]
    public void SetIisRegistrySettings_AddsKeyIfItAlreadyExists()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key1 = GetRegistryKey();
        var key2 = GetRegistryKey();
        Registry.LocalMachine.CreateSubKey(key1).Should().NotBeNull();
        Registry.LocalMachine.CreateSubKey(key2).Should().NotBeNull();

        RegistryHelper.SetIisRegistrySettings(Log, values, key1, key2)
                      .Should()
                      .BeTrue();

        var expected = values.IisRequiredEnvVariables.Select(kvp => kvp + "=" + kvp.Value).ToArray();
        expected.Should().HaveSameCount(values.IisRequiredEnvVariables);
        VerifyIisRegKey(key1, expected);
        VerifyIisRegKey(key2, expected);
    }

    [SkippableFact]
    public void SetIisRegistrySettings_OverwritesExistingValueIfSet()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var home = CreateMonitoringHomeCopy();
        var values = new TracerValues(home);
        var key1 = GetRegistryKey();
        var key2 = GetRegistryKey();

        // First key with the "wrong" value
        var regKey1 = Registry.LocalMachine.CreateSubKey(key1);
        regKey1.Should().NotBeNull();
        regKey1!.SetValue(IisRegKeyValueName, 0, RegistryValueKind.DWord);

        // This one has a different wrong value
        var regKey2 = Registry.LocalMachine.CreateSubKey(key2);
        regKey2.Should().NotBeNull();
        regKey2!.SetValue(IisRegKeyValueName, "Blah=Blip", RegistryValueKind.String);

        RegistryHelper.SetIisRegistrySettings(Log, values, key1, key2)
                      .Should()
                      .BeTrue();

        var expected = values.IisRequiredEnvVariables.Select(kvp => kvp + "=" + kvp.Value).ToArray();
        expected.Should().HaveSameCount(values.IisRequiredEnvVariables);
        VerifyIisRegKey(key1, expected);
        VerifyIisRegKey(key2, expected);
    }

    [SkippableFact]
    public void RemoveIisRegistrySettings_RemovesValueIfSet()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var key1 = GetRegistryKey();
        var key2 = GetRegistryKey();

        // Add keys with real values for now
        var regKey1 = Registry.LocalMachine.CreateSubKey(key1);
        regKey1!.SetValue(IisRegKeyValueName, new[] { "Blah=blip", "Bleep=bloop" }, RegistryValueKind.MultiString);
        var regKey2 = Registry.LocalMachine.CreateSubKey(key2);
        regKey2!.SetValue(IisRegKeyValueName, new[] { "Blah=blip", "Bleep=bloop" }, RegistryValueKind.MultiString);

        RegistryHelper.RemoveIisRegistrySettings(Log, key1, key2)
                      .Should()
                      .BeTrue();

        // verify the key value is no longer there
        Registry.LocalMachine.OpenSubKey(key1)?.GetValue(IisRegKeyValueName).Should().BeNull();
        Registry.LocalMachine.OpenSubKey(key2)?.GetValue(IisRegKeyValueName).Should().BeNull();
    }

    [SkippableFact]
    public void RemoveIisRegistrySettings_ReturnsTrueIfKeyDoesntExist()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var key1 = GetRegistryKey();
        var key2 = GetRegistryKey();

        // Add keys with real values for now
        Registry.LocalMachine.CreateSubKey(key1).Should().NotBeNull();
        Registry.LocalMachine.CreateSubKey(key2).Should().NotBeNull();

        RegistryHelper.RemoveIisRegistrySettings(Log, key1, key2)
                      .Should()
                      .BeTrue();

        // verify the key value is no longer there
        Registry.LocalMachine.OpenSubKey(key1)?.GetValue(IisRegKeyValueName).Should().BeNull();
        Registry.LocalMachine.OpenSubKey(key2)?.GetValue(IisRegKeyValueName).Should().BeNull();
    }

    [SkippableFact]
    public void RemoveIisRegistrySettings_ReturnsTrueIfKeyIsNotThere()
    {
        // We skip the test if we don't have permissions
        Skip.IfNot(IsRunningAsAdministrator);

        var key1 = GetRegistryKey();
        var key2 = GetRegistryKey();

        // Keys should not exist
        Registry.LocalMachine.OpenSubKey(key1).Should().BeNull();
        Registry.LocalMachine.OpenSubKey(key2).Should().BeNull();

        RegistryHelper.RemoveIisRegistrySettings(Log, key1, key2)
                      .Should()
                      .BeTrue();

        // verify the key value is no longer there
        Registry.LocalMachine.OpenSubKey(key1)?.GetValue(IisRegKeyValueName).Should().BeNull();
        Registry.LocalMachine.OpenSubKey(key2)?.GetValue(IisRegKeyValueName).Should().BeNull();
    }

    private static void VerifyIisRegKey(string key1, string[] expected)
    {
        Registry.LocalMachine.OpenSubKey(key1)
               ?.GetValue(IisRegKeyValueName)
                .Should()
                .BeOfType<string[]>()
                .Which.Should().BeEquivalentTo(expected);
    }

    private string GetRegistryKey()
    {
        var key = $@"SOFTWARE\Datadog\Datadog .NET Tracer\{Guid.NewGuid():N}";
        _keysToDelete.Add(key);
        return key;
    }
}

#endif
