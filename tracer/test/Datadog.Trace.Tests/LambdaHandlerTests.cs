// <copyright file="LambdaHandlerTests.cs" company="Datadog">
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
    public class LambdaHandlerTests
    {
        [Fact]
        public void LambdaHandlerGetters()
        {
            LambdaHandler handler = new LambdaHandler("test-assembly::test-type::test-method");
            handler.GetAssembly().Should().Be("test-assembly");
            handler.GetFullType().Should().Be("test-type");
            handler.GetMethodName().Should().Be("test-method");
        }

        [Fact]
        public void LambdaHandlerBuidParamTypeArray()
        {
            LambdaHandler handler = new LambdaHandler("System.Runtime::Environment::GetEnvironmentVariable");
            handler.GetParamTypeArray().Length.Sould().Be(2);
            handler.GetParamTypeArray()[0].Sould().Be("String");
            handler.GetParamTypeArray()[1].Sould().Be("String");
        }
    }
}
