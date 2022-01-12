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
        [Fact]
        public void IsRunningInLambdaFalseNoFileAndNoEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", string.Empty);
            string path = Directory.GetCurrentDirectory() + "/invalid";
            Serverless.IsRunningInLambda(path).Should().Be(false);
        }

        [Fact]
        public void IsRunningInLambdaFalseNoFileAndEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "my-test-function");
            string path = Directory.GetCurrentDirectory() + "/invalid";
            Serverless.IsRunningInLambda(path).Should().Be(false);
        }

        [Fact]
        public void IsRunningInLambdaFalseFileAndNoEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", string.Empty);
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];
            Serverless.IsRunningInLambda(existingFile).Should().Be(false);
        }

        [Fact]
        public void IsRunningInLambdaTrue()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "my-test-function");
            string currentDirectory = Directory.GetCurrentDirectory();
            string existingFile = Directory.GetFiles(currentDirectory)[0];
            Serverless.IsRunningInLambda(existingFile).Should().Be(true);
        }
    }
}
