// <copyright file="RegistryServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Checks;
using Datadog.Trace.Tools.Runner.Checks.Windows;
using FluentAssertions;
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
        public void GetLocalMachineSubKeyVersion()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            // Mock data to check
            const string testKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
            const string displayName = "TestDisplayName";
            const string versionMajor = "1";
            const string versionMinor = "0";

            var registryService = MockRegistryService(testKey, displayName);
            var result = registryService.GetLocalMachineSubKeyVersion(testKey, displayName, out var tracerVersion);

            result.Should().BeTrue();
            tracerVersion.Should().Be(versionMajor + "." + versionMinor);
        }

        private static IRegistryService MockRegistryService(string key, string displayName)
        {
            var registryService = new Mock<IRegistryService>();
            const string testKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

            registryService.Setup(r => r.GetLocalMachineSubKeyVersion(
                                      It.Is<string>(k => k == key),
                                      It.Is<string>(d => d == displayName),
                                      out It.Ref<string>.IsAny))
                           .Callback((string keyName, string nameValue, out string version) =>
                            {
                                if (testKey == keyName && nameValue == "TestDisplayName")
                                {
                                    version = "1.0";
                                }
                                else
                                {
                                    version = null;
                                }
                            })
                           .Returns(true);

            return registryService.Object;
        }
    }
}
