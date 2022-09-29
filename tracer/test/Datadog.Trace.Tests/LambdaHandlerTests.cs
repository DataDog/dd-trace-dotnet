// <copyright file="LambdaHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.TestHelpers;
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
            handler.Assembly.Should().Be("mscorlib");
            handler.FullType.Should().Be("System.Environment");
            handler.MethodName.Should().Be("ExpandEnvironmentVariables");
        }

        [Fact]
        public void LambdaHandlerParamTypeArray()
        {
            LambdaHandler handler = new LambdaHandler("mscorlib::System.Environment::ExpandEnvironmentVariables");
            handler.ParamTypeArray.Length.Should().Be(2);
            handler.ParamTypeArray[0].Should().Be("System.String");
            handler.ParamTypeArray[1].Should().Be("System.String");
        }

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::HandlerMethod", "Datadog.Trace.Tests.TestHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::HiddenBaseMethod", "Datadog.Trace.Tests.TestHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::OverridenBaseMethod", "Datadog.Trace.Tests.TestHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler2::AbstractBaseMethod", "Datadog.Trace.Tests.TestHandler2")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::BaseHandlerMethod", "Datadog.Trace.Tests.BaseHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::NonOverridenBaseMethod", "Datadog.Trace.Tests.BaseHandler")]
        public void LambdaHandlerCanParseCustomType(string handlerVariable, string expectedType)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.FullType.Should().Be(expectedType);
            handler.ParamTypeArray.Length.Should().Be(1);
            handler.ParamTypeArray[0].Should().Be(ClrNames.Void);
        }

        [Fact]
        public void LambdaHandlerCanParseTypesAcrossAssemblies()
        {
            var handlerName = "Datadog.Trace.Tests::Datadog.Trace.Tests.TestMockSpan::GetTag";
            LambdaHandler handler = new LambdaHandler(handlerName);
            handler.Assembly.Should().Be("Datadog.Trace.TestHelpers");
            handler.FullType.Should().Be("Datadog.Trace.TestHelpers.MockSpan");
            handler.MethodName.Should().Be("GetTag");
            handler.ParamTypeArray.Length.Should().Be(2);
            handler.ParamTypeArray[0].Should().Be(ClrNames.String);
            handler.ParamTypeArray[1].Should().Be(ClrNames.String);
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class TestHandler : BaseHandler
    {
        public void HandlerMethod()
        {
        }

        public new void HiddenBaseMethod()
        {
        }

        public override void OverridenBaseMethod()
        {
        }
    }

    public class TestHandler2 : AbstractBaseHandler
    {
        public override void AbstractBaseMethod()
        {
        }
    }

    public class TestHandler3 : AbstractGenericBaseHandler<int>
    {
        public override void AbstractBaseMethod()
        {
        }
    }

    public class BaseHandler
    {
        public void BaseHandlerMethod()
        {
        }

        public void HiddenBaseMethod()
        {
        }

        public virtual void NonOverridenBaseMethod()
        {
        }

        public virtual void OverridenBaseMethod()
        {
        }
    }

    public abstract class AbstractBaseHandler
    {
        public abstract void AbstractBaseMethod();
    }

    public abstract class AbstractGenericBaseHandler<T>
    {
        public abstract void AbstractBaseMethod();
    }

    public class TestMockSpan : MockSpan
    {
    }
}
