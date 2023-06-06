// <copyright file="ServerlessTests.cs" company="Datadog">
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
    public class ServerlessTests : IDisposable
    {
        private const string FunctionNameEnvVar = "AWS_LAMBDA_FUNCTION_NAME";
        private const string HandlerEnvVar = "_HANDLER";
        private readonly Dictionary<string, string> _originalEnvVars;

        public ServerlessTests()
        {
            _originalEnvVars = new()
            {
                { FunctionNameEnvVar, Environment.GetEnvironmentVariable(FunctionNameEnvVar) },
                { HandlerEnvVar, Environment.GetEnvironmentVariable(HandlerEnvVar) },
                { "FUNCTION_NAME", Environment.GetEnvironmentVariable("FUNCTION_NAME") },
                { "GCP_PROJECT", Environment.GetEnvironmentVariable("GCP_PROJECT") },
                { "K_SERVICE", Environment.GetEnvironmentVariable("K_SERVICE") },
                { "FUNCTION_TARGET", Environment.GetEnvironmentVariable("FUNCTION_TARGET") },
                { "AzureWebJobsScriptRoot", Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot") },
                { "FUNCTIONS_EXTENSION_VERSION", Environment.GetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION") },
            };
        }

        public void Dispose()
        {
            foreach (var originalEnvVar in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(originalEnvVar.Key, originalEnvVar.Value);
            }

            Serverless.SetIsGCPAzureEnvVarsTestsOnly();
        }

        [Fact]
        public void IsRunningInLambdaFalseNoFileAndNoEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, string.Empty);
            string path = Directory.GetCurrentDirectory() + "/invalid";
            var res = Serverless.LambdaMetadata.Create(path);
            res.IsRunningInLambda.Should().BeFalse();
        }

        [Fact]
        public void IsRunningInLambdaFalseNoFileAndEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, "my-test-function");
            string path = Directory.GetCurrentDirectory() + "/invalid";
            var res = Serverless.LambdaMetadata.Create(path);
            res.IsRunningInLambda.Should().BeFalse();
        }

        [Fact]
        public void IsRunningInLambdaFalseFileAndNoEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, string.Empty);
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];
            var res = Serverless.LambdaMetadata.Create(existingFile);
            res.IsRunningInLambda.Should().BeFalse();
        }

        [Fact]
        public void IsRunningInLambdaTrue()
        {
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, "my-test-function");
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];
            var res = Serverless.LambdaMetadata.Create(existingFile);
            res.IsRunningInLambda.Should().BeTrue();
            res.FunctionName.Should().Be("my-test-function");
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("SomeValue", "SomeValue")]
        public void Extracts_Handler(string handler, string expectedHandler)
        {
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, "my-test-function");
            Environment.SetEnvironmentVariable(HandlerEnvVar, handler);
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];

            var res = Serverless.LambdaMetadata.Create(existingFile);

            res.IsRunningInLambda.Should().BeTrue();
            res.FunctionName.Should().Be("my-test-function");
            res.HandlerName.Should().Be(expectedHandler);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("SomeValue", "SomeValue")]
        [InlineData("::Invalid::Name", null)]
        [InlineData("AssemblyName::Some::Value", "AssemblyName")]
        [InlineData("AssemblyNameNotValidButOk::", "AssemblyNameNotValidButOk")]
        public void Extracts_ServiceFromHandler(string handler, string expectedService)
        {
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, "my-test-function");
            Environment.SetEnvironmentVariable(HandlerEnvVar, handler);
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];

            var res = Serverless.LambdaMetadata.Create(existingFile);

            res.IsRunningInLambda.Should().BeTrue();
            res.FunctionName.Should().Be("my-test-function");
            res.ServiceName.Should().Be(expectedService);
        }

        [Fact]
        public void GetSyncIntegrationTypeFromParamCountZero()
        {
            Serverless.GetSyncIntegrationTypeFromParamCount(0).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParamSync");
        }

        [Fact]
        public void GetSyncIntegrationTypeFromParamCountOne()
        {
            Serverless.GetSyncIntegrationTypeFromParamCount(1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaOneParamSync");
        }

        [Fact]
        public void GetSyncIntegrationTypeFromParamCountTwo()
        {
            Serverless.GetSyncIntegrationTypeFromParamCount(2).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaTwoParamsSync");
        }

        [Fact]
        public void GetSyncIntegrationTypeFromParamCountMoreThanExpected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Serverless.GetSyncIntegrationTypeFromParamCount(3));
        }

        [Fact]
        public void GetAsyncIntegrationTypeFromParamCountZero()
        {
            Serverless.GetAsyncIntegrationTypeFromParamCount(0).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParamAsync");
        }

        [Fact]
        public void GetAsyncIntegrationTypeFromParamCountOne()
        {
            Serverless.GetAsyncIntegrationTypeFromParamCount(1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaOneParamAsync");
        }

        [Fact]
        public void GetAsyncIntegrationTypeFromParamCountTwo()
        {
            Serverless.GetAsyncIntegrationTypeFromParamCount(2).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaTwoParamsAsync");
        }

        [Fact]
        public void GetAsyncIntegrationTypeFromParamCountMoreThanExpected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Serverless.GetAsyncIntegrationTypeFromParamCount(3));
        }

        [Fact]
        public void GetVoidIntegrationTypeFromParamCountZero()
        {
            Serverless.GetVoidIntegrationTypeFromParamCount(0).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParamVoid");
        }

        [Fact]
        public void GetVoidIntegrationTypeFromParamCountOne()
        {
            Serverless.GetVoidIntegrationTypeFromParamCount(1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaOneParamVoid");
        }

        [Fact]
        public void GetVoidIntegrationTypeFromParamCountTwo()
        {
            Serverless.GetVoidIntegrationTypeFromParamCount(2).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaTwoParamsVoid");
        }

        [Fact]
        public void GetVoidIntegrationTypeFromParamCountMoreThanExpected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Serverless.GetAsyncIntegrationTypeFromParamCount(3));
        }

        [Fact]
        public void GetIntegrationTypeSync()
        {
            Serverless.GetIntegrationType("System.String", 1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParamSync");
        }

        [Fact]
        public void GetIntegrationTypeAsync()
        {
            Serverless.GetIntegrationType("System.Threading.Tasks.Task<System.String>", 1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParamAsync");
        }

        [Fact]
        public void GetIntegrationTypeVoid()
        {
            Serverless.GetIntegrationType("System.Void", 1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParamVoid");
        }

        [Fact]
        public void DoesntSpawnMiniAgentInNonFunctionEnvironments()
        {
            Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
            Environment.SetEnvironmentVariable("K_SERVICE", null);
            Environment.SetEnvironmentVariable("AzureWebJobsScriptRoot", null);

            var miniAgentManagerMock = new Mock<MiniAgentManager>();
            Serverless.MaybeStartMiniAgent(miniAgentManagerMock.Object);
            miniAgentManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void SpawnMiniAgentInDeprecatedGCPFunction()
        {
            Environment.SetEnvironmentVariable("FUNCTION_NAME", "dummy_function");
            Environment.SetEnvironmentVariable("GCP_PROJECT", "dummy_project");

            Serverless.SetIsGCPAzureEnvVarsTestsOnly();

            var miniAgentManagerMock = new Mock<MiniAgentManager>();

            Serverless.MaybeStartMiniAgent(miniAgentManagerMock.Object);

            miniAgentManagerMock.Verify(m => m.Start(It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public void SpawnMiniAgentInNewerGCPFunction()
        {
            Environment.SetEnvironmentVariable("K_SERVICE", "dummy_function");
            Environment.SetEnvironmentVariable("FUNCTION_TARGET", "dummy_target");
            Serverless.SetIsGCPAzureEnvVarsTestsOnly();

            var miniAgentManagerMock = new Mock<MiniAgentManager>();

            Serverless.MaybeStartMiniAgent(miniAgentManagerMock.Object);

            miniAgentManagerMock.Verify(m => m.Start(It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public void SpawnMiniAgentInAzureFunction()
        {
            Environment.SetEnvironmentVariable("AzureWebJobsScriptRoot", "/home/site/wwwroot");
            Environment.SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "4");
            Serverless.SetIsGCPAzureEnvVarsTestsOnly();

            var miniAgentManagerMock = new Mock<MiniAgentManager>();

            Serverless.MaybeStartMiniAgent(miniAgentManagerMock.Object);

            miniAgentManagerMock.Verify(m => m.Start(It.IsAny<string>()), Times.Once());
        }
    }
}
