// <copyright file="ServerlessTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class ServerlessTests
    {
        private const string FunctionNameEnvVar = "AWS_LAMBDA_FUNCTION_NAME";

        [Fact]
        public void IsRunningInLambdaFalseNoFileAndNoEnvironmentVariable()
        {
            var originalValue = Environment.GetEnvironmentVariable(FunctionNameEnvVar);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, string.Empty);
            string path = Directory.GetCurrentDirectory() + "/invalid";
            var res = Serverless.IsRunningInLambda(path);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, originalValue);
            res.Should().Be(false);
        }

        [Fact]
        public void IsRunningInLambdaFalseNoFileAndEnvironmentVariable()
        {
            var originalValue = Environment.GetEnvironmentVariable(FunctionNameEnvVar);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, "my-test-function");
            string path = Directory.GetCurrentDirectory() + "/invalid";
            var res = Serverless.IsRunningInLambda(path);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, originalValue);
            res.Should().Be(false);
        }

        [Fact]
        public void IsRunningInLambdaFalseFileAndNoEnvironmentVariable()
        {
            var originalValue = Environment.GetEnvironmentVariable(FunctionNameEnvVar);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, string.Empty);
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];
            var res = Serverless.IsRunningInLambda(existingFile);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, originalValue);
            res.Should().Be(false);
        }

        [Fact]
        public void IsRunningInLambdaTrue()
        {
            var originalValue = Environment.GetEnvironmentVariable(FunctionNameEnvVar);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, "my-test-function");
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];
            var res = Serverless.IsRunningInLambda(existingFile);
            Environment.SetEnvironmentVariable(FunctionNameEnvVar, originalValue);
            res.Should().Be(true);
        }

        [Fact]
        public void GetIntegrationTypeFromParamCountZero()
        {
            Serverless.GetIntegrationTypeFromParamCount(1).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaNoParam");
        }

        [Fact]
        public void GetIntegrationTypeFromParamCountOne()
        {
            Serverless.GetIntegrationTypeFromParamCount(2).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaOneParam");
        }

        [Fact]
        public void GetIntegrationTypeFromParamCountTwo()
        {
            Serverless.GetIntegrationTypeFromParamCount(3).Should().Be("Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.LambdaTwoParams");
        }

        [Fact]
        public void GetIntegrationTypeFromParamCountMoreThanExpected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Serverless.GetIntegrationTypeFromParamCount(5));
        }
    }
}
