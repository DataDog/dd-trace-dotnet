// <copyright file="RegistryServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.dd_dotnet.Checks.Windows;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tools.Shared.Tests
{
    public class RegistryServiceTests
    {
        [Fact]
        public void DontCrash()
        {
            var result = new RegistryService().GetLocalMachineValueNames("DummyKey");

            result.Should().BeEmpty();
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public void GetLocalMachineKeyNames()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            const string testKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

            var registryService = MockRegistryService(testKey, Array.Empty<string>(), null, null, null);
            var result = registryService.GetLocalMachineKeyNames(testKey);

            result.Should().BeEquivalentTo(Array.Empty<string>());
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public void GetLocalMachineKeyNameValue()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            const string testKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
            const string subKeyName = "TestSubKey";
            const string valueName = "TestValue";
            const string expectedValue = "TestValueData";

            var registryService = MockRegistryService(testKey, null, subKeyName, valueName, expectedValue);
            var result = registryService.GetLocalMachineKeyNameValue(testKey, subKeyName, valueName);

            result.Should().Be(expectedValue);
        }

        private static IRegistryService MockRegistryService(string key, string[] subKeyNames, string subKeyName, string valueName, string value)
        {
            var registryService = new Mock<IRegistryService>();

            registryService.Setup(r => r.GetLocalMachineKeyNames(
                                      It.Is<string>(k => k == key)))
                           .Returns(subKeyNames);
            registryService.Setup(r => r.GetLocalMachineKeyNameValue(
                                      It.Is<string>(k => k == key),
                                      It.Is<string>(sk => sk == subKeyName),
                                      It.Is<string>(n => n == valueName)))
                           .Returns(value);

            return registryService.Object;
        }
    }
}
