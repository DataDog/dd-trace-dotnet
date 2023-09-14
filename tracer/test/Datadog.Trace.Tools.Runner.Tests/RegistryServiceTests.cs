// <copyright file="RegistryServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Checks;
using Datadog.Trace.Tools.Runner.Checks.Windows;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests
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
        public void GetLocalMachineSubKeyVersion_ReturnsVersion()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            // Mock data to check
            const string testKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
            const string displayName = "TestDisplayName";
            const string versionMajor = "1";
            const string versionMinor = "0";

            // Creating mock registry
            var mockRegistryKey = new Mock<RegistryKey>();
            mockRegistryKey.Setup(key => key.GetSubKeyNames()).Returns(new[] { "SubKey1" });

            var mockSubKey = new Mock<RegistryKey>();
            mockSubKey.Setup(key => key.GetValue("DisplayName")).Returns(displayName);
            mockSubKey.Setup(key => key.GetValue("VersionMajor")).Returns(versionMajor);
            mockSubKey.Setup(key => key.GetValue("VersionMinor")).Returns(versionMinor);

            var mockLocalMachine = new Mock<RegistryKey>();
            mockLocalMachine.Setup(key => key.OpenSubKey(testKey)).Returns(mockRegistryKey.Object);

            // Passing mock registry and checking output of method
            var registryService = new RegistryService(mockLocalMachine.Object);
            bool versionFound = registryService.GetLocalMachineSubKeyVersion(testKey, displayName, out string tracerVersion);

            Assert.True(versionFound);
            Assert.Equal($"{versionMajor}.{versionMinor}", tracerVersion);
        }
    }
}
