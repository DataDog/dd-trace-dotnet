// <copyright file="ServerlessMiniAgentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ServerlessMiniAgentTests : IDisposable
    {
        private readonly Dictionary<string, string> _originalEnvVars;

        public ServerlessMiniAgentTests()
        {
            _originalEnvVars = new()
            {
                { ServerlessMiniAgent.AzureFunctionNameEnvVar, Environment.GetEnvironmentVariable(ServerlessMiniAgent.AzureFunctionNameEnvVar) },
                { ServerlessMiniAgent.AzureFunctionIdentifierEnvVar, Environment.GetEnvironmentVariable(ServerlessMiniAgent.AzureFunctionIdentifierEnvVar) },
                { ServerlessMiniAgent.GCPFunctionDeprecatedNameEnvVar, Environment.GetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionDeprecatedNameEnvVar) },
                { ServerlessMiniAgent.GCPFunctionDeprecatedEnvVarIdentifier, Environment.GetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionDeprecatedEnvVarIdentifier) },
                { ServerlessMiniAgent.GCPFunctionNewerNameEnvVar, Environment.GetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionNewerNameEnvVar) },
                { ServerlessMiniAgent.GCPFunctionNewerEnvVarIdentifier, Environment.GetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionNewerEnvVarIdentifier) },
            };
        }

        public void Dispose()
        {
            foreach (var originalEnvVar in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(originalEnvVar.Key, originalEnvVar.Value);
            }

            ServerlessMiniAgent.UpdateIsGCPAzureEnvVarsTestsOnly();
        }

        [Fact]
        public void DoesntSpawnMiniAgentInNonFunctionEnvironments()
        {
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionDeprecatedEnvVarIdentifier, null);
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionNewerEnvVarIdentifier, null);
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.AzureFunctionIdentifierEnvVar, null);

            var miniAgentManagerMock = new Mock<ServerlessMiniAgentManager>();
            ServerlessMiniAgent.MaybeStartMiniAgent(miniAgentManagerMock.Object);
            miniAgentManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void SpawnMiniAgentInDeprecatedGCPFunction()
        {
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionDeprecatedNameEnvVar, "dummy_function");
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionDeprecatedEnvVarIdentifier, "dummy_project");
            ServerlessMiniAgent.UpdateIsGCPAzureEnvVarsTestsOnly();

            var miniAgentManagerMock = new Mock<ServerlessMiniAgentManager>();

            ServerlessMiniAgent.MaybeStartMiniAgent(miniAgentManagerMock.Object);

            miniAgentManagerMock.Verify(m => m.Start(It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public void SpawnMiniAgentInNewerGCPFunction()
        {
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionNewerNameEnvVar, "dummy_function");
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.GCPFunctionNewerEnvVarIdentifier, "dummy_target");
            ServerlessMiniAgent.UpdateIsGCPAzureEnvVarsTestsOnly();

            var miniAgentManagerMock = new Mock<ServerlessMiniAgentManager>();

            ServerlessMiniAgent.MaybeStartMiniAgent(miniAgentManagerMock.Object);

            miniAgentManagerMock.Verify(m => m.Start(It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public void SpawnMiniAgentInAzureFunction()
        {
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.AzureFunctionNameEnvVar, "function_name");
            Environment.SetEnvironmentVariable(ServerlessMiniAgent.AzureFunctionIdentifierEnvVar, "4");
            ServerlessMiniAgent.UpdateIsGCPAzureEnvVarsTestsOnly();

            var miniAgentManagerMock = new Mock<ServerlessMiniAgentManager>();

            ServerlessMiniAgent.MaybeStartMiniAgent(miniAgentManagerMock.Object);

            miniAgentManagerMock.Verify(m => m.Start(It.IsAny<string>()), Times.Once());
        }
    }
}
