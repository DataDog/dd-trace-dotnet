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
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler3::AbstractBaseMethod", "Datadog.Trace.Tests.TestHandler3")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::BaseHandlerMethod", "Datadog.Trace.Tests.BaseHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::NonOverridenBaseMethod", "Datadog.Trace.Tests.BaseHandler")]
        public void LambdaHandlerCanParseCustomType(string handlerVariable, string expectedType)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.FullType.Should().Be(expectedType);
            handler.ParamTypeArray.Length.Should().Be(1);
            handler.ParamTypeArray[0].Should().Be(ClrNames.Void);
        }

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler3::AbstractGenericBaseMethod1", "Datadog.Trace.Tests.TestHandler3", "AbstractGenericBaseMethod1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler3::AbstractGenericBaseMethod2", "Datadog.Trace.Tests.TestHandler3", "AbstractGenericBaseMethod2")]
        public void LambdaHandlerCanHandleClosedGenericTypes(string handlerVariable, string expectedType, string expectedMethod)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be(expectedType);
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Length.Should().Be(2);
            handler.ParamTypeArray[0].Should().Be(ClrNames.Int32);
            handler.ParamTypeArray[1].Should().Be(ClrNames.Int32);
        }

        [Theory(Skip = "We don't currently support open generics in Lambda")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod1", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod2", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod3", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        public void LambdaHandlerCanHandleOpenGenericTypes(string handlerVariable, string expectedType, string expectedMethod, params string[] args)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be(expectedType);
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Should().BeSameAs(args);
        }

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler+NestedHandler::HandlerMethod", "Datadog.Trace.Tests.TestHandler+NestedHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler+NestedHandler::HiddenBaseMethod", "Datadog.Trace.Tests.TestHandler+NestedHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler+NestedHandler::OverridenBaseMethod", "Datadog.Trace.Tests.TestHandler+NestedHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler+NestedHandler::BaseHandlerMethod", "Datadog.Trace.Tests.BaseHandler")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler+NestedHandler::NonOverridenBaseMethod", "Datadog.Trace.Tests.BaseHandler")]
        public void LambdaHandlerCanHandleNestedTypes(string handlerVariable, string expectedType)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
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

        public class NestedHandler : BaseHandler
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

        public override int AbstractGenericBaseMethod1(int value) => 1;

        public override int AbstractGenericBaseMethod2(int value) => 1;
    }

    public class TestHandler4 : GenericBaseHandler<int>
    {
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

        public abstract int AbstractGenericBaseMethod1(T value);

        public abstract T AbstractGenericBaseMethod2(int value);
    }

    public class GenericBaseHandler<T>
    {
        public void GenericBaseMethod1(T value)
        {
        }

        public T GenericBaseMethod2() => default;

        public T2 GenericBaseMethod3<T2>() => default;
    }

    public class TestMockSpan : MockSpan
    {
    }
}
