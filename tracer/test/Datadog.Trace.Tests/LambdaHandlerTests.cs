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
            LambdaHandler handler = new LambdaHandler("mscorlib::System.Environment::ExpandEnvironmentVariables");
            handler.GetAssembly().Should().Be("mscorlib");
            handler.GetFullType().Should().Be("System.Environment");
            handler.GetMethodName().Should().Be("ExpandEnvironmentVariables");
        }

        [Fact]
        public void LambdaHandlerBuidParamTypeArray()
        {
            LambdaHandler handler = new LambdaHandler("mscorlib::System.Environment::ExpandEnvironmentVariables");
            handler.ParamTypeArray.Length.Should().Be(2);
            handler.ParamTypeArray[0].Should().Be("System.String");
            handler.ParamTypeArray[1].Should().Be("System.String");
        }
    }
}
