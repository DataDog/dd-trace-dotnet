// <copyright file="LambdaHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
#pragma warning disable SA1402 // File may only contain a single type
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

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.ShadowTest::AbstractFunctionInGeneric", "Datadog.Trace.Tests.ShadowTest", "AbstractFunctionInGeneric", ClrNames.String, ClrNames.String)]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.ShadowTest::VirtualFunctionInGeneric", "Datadog.Trace.Tests.ShadowTest", "VirtualFunctionInGeneric", ClrNames.String, ClrNames.String)]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.ShadowTest::ShadowedFunctionInGeneric", "Datadog.Trace.Tests.ShadowTest", "ShadowedFunctionInGeneric", ClrNames.String, ClrNames.String)]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.ShadowTest+Nested::ShadowedFunctionInGeneric", "Datadog.Trace.Tests.ShadowTest+Nested", "ShadowedFunctionInGeneric", ClrNames.String, ClrNames.String)]
        public void LambdaHandlerCanHandleShadowing(string handlerVariable, string expectedType, string expectedMethod, params string[] args)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be(expectedType);
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Should().BeEquivalentTo(args, opts => opts.WithStrictOrdering());
        }

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod1", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1", ClrNames.Void, "!0")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod2", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2", "!0")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod1", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod1", ClrNames.Void, "!0")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod2", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod2", "!0")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod3", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod3", ClrNames.Void, "!1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod4", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod4", "!1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod5", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod5", "!1", "!0")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod6", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod6", ClrNames.Void, "!0", "!1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod7", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod7", "System.Threading.Tasks.Task`1[!0]", "System.Threading.Tasks.Task`1[!1]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler6::GenericBaseMethod5", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod5", "!1", "!0")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler6::GenericBaseMethod6", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod6", ClrNames.Void, "!0", "!1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler6::GenericBaseMethod7", "Datadog.Trace.Tests.GenericBaseHandler2`2", "GenericBaseMethod7", "System.Threading.Tasks.Task`1[!0]", "System.Threading.Tasks.Task`1[!1]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler7+Nested::Handler", "Datadog.Trace.Tests.TrickyParamHandler+NestedGeneric`2", "Handler", "System.Threading.Tasks.Task`1[System.Tuple`2[!0,NestedGeneric`2[!0,!1]]]", "System.Tuple`2[!0,NestedGeneric`2[!0,!1]]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.FunctionImplementation::FunctionHandlerAsync", "Datadog.Trace.Tests.AbstractAspNetCoreFunction`2", "FunctionHandlerAsync", "System.Threading.Tasks.Task`1[!1]", "!0", "Datadog.Trace.Tests.ILambdaContext")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.DerivedImplementation::ComplexNestedGeneric", "Datadog.Trace.Tests.GenericBase`2", "ComplexNestedGeneric", "Datadog.Trace.Tests.GenericBase`2[Datadog.Trace.Tests.CustomInput,InnerGeneric`2[!0,!1]]", "!0", "InnerGeneric`2[!0,NestedInSameType`2[!0,!1,!0,System.Collections.Generic.Dictionary`2[System.String,DeepNested`1[!0,!1,!1]]]]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.DerivedImplementation::NonGenericFunction", "Datadog.Trace.Tests.GenericBase`2", "NonGenericFunction", ClrNames.Task, ClrNames.String, "Datadog.Trace.Tests.ILambdaContext")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.DerivedImplementation+NestedDerived::ComplexNestedGeneric", "Datadog.Trace.Tests.GenericBase`2", "ComplexNestedGeneric", "Datadog.Trace.Tests.GenericBase`2[Datadog.Trace.Tests.CustomInput,InnerGeneric`2[!0,!1]]", "!0", "InnerGeneric`2[!0,NestedInSameType`2[!0,!1,!0,System.Collections.Generic.Dictionary`2[System.String,DeepNested`1[!0,!1,!1]]]]")]
        public void LambdaHandlerCanHandleOpenGenericTypes(string handlerVariable, string expectedType, string expectedMethod, params string[] args)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be(expectedType);
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Should().BeEquivalentTo(args, opts => opts.WithStrictOrdering());
        }

        [Theory]
        [InlineData("")]
        [InlineData("::")]
        [InlineData("A::B")]
        [InlineData("A:::B")]
        [InlineData("A::B::C::")]
        [InlineData("A::B::C::D")]
        public void LambdaHandlerThrowsWhenInvalidFormat(string handlerVariable)
        {
            Assert.Throws<ArgumentException>(() => new LambdaHandler(handlerVariable));
        }

        [Theory]
        [InlineData("SomeType::Unknown::WhoKnows")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler::IdontExistAnywhere")]
        [InlineData("SomeType::::WhoKnows")]
        [InlineData("SomeType::::")]
        public void LambdaHandlerThrowsWhenUnknownTypes(string handlerVariable)
        {
            Assert.Throws<ArgumentException>(() => new LambdaHandler(handlerVariable));
        }

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod3")]
        public void LambdaHandlerThrowsWhenInstrumentingGenericMethods(string handlerVariable)
        {
            // We don't support this yet (we could, we just haven't done the work for it yet)
            Assert.Throws<ArgumentException>(() => new LambdaHandler(handlerVariable));
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

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::GenericArguments", "GenericArguments", "System.Collections.Generic.Dictionary`2[System.String,System.String]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::NestedClassArgument", "NestedClassArgument", "NestedClass")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::DoublyNestedClassArgument", "DoublyNestedClassArgument", "Inner")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::NestedStructArgument", "NestedStructArgument", "NestedStruct")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::NestedGenericArguments", "NestedGenericArguments", "NestedGeneric`2[System.String,System.String]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::DoublyNestedGenericArguments", "DoublyNestedGenericArguments", "InnerGeneric`2[System.String,System.String]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::RecursiveGenericArguments", "RecursiveGenericArguments", "System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.Dictionary`2[System.String,System.String]]")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TrickyParamHandler::NestedRecursiveGenericArguments", "NestedRecursiveGenericArguments", "NestedGeneric`2[System.String,NestedGeneric`2[System.String,System.Collections.Generic.Dictionary`2[System.String,System.String]]]")]
        public void LambdaHandlerCanHandleTrickyArguments(string handlerVariable, string expectedMethod, string expectedArg)
        {
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be("Datadog.Trace.Tests.TrickyParamHandler");
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Length.Should().Be(2);
            handler.ParamTypeArray[0].Should().Be(ClrNames.Int32);
            handler.ParamTypeArray[1].Should().Be(expectedArg);
        }
    }

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

    public class TestHandler5 : GenericBaseHandler2<int, string>
    {
    }

    public class TestHandler6 : GenericBaseHandler2<int, int>
    {
    }

    public class TestHandler7
    {
        public class Nested : TrickyParamHandler.NestedGeneric<int, string>
        {
        }
    }

    public class ShadowTest : GenericAbstractBase<string>
    {
        public override string AbstractFunctionInGeneric(string arg) => arg;

        public override string VirtualFunctionInGeneric(string arg) => arg;

        public string ShadowedFunctionInGeneric(string arg) => arg;

        public class Nested : ShadowTest
        {
            public new string ShadowedFunctionInGeneric(string arg) => arg;
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

        public abstract int AbstractGenericBaseMethod1(T value);

        public abstract T AbstractGenericBaseMethod2(int value);
    }

    public class AbstractAspNetCoreFunction
    {
    }

    public class AbstractAspNetCoreFunction<TREQUEST, TRESPONSE> : AbstractAspNetCoreFunction
    {
        public virtual async Task<TRESPONSE> FunctionHandlerAsync(TREQUEST request, ILambdaContext lambdaContext)
        {
            return await Task.FromResult<TRESPONSE>(default(TRESPONSE));
        }
    }

    public class FunctionImplementation : AbstractAspNetCoreFunction<string, string>
    {
    }

    public class GenericBaseHandler<T>
    {
        public void GenericBaseMethod1(T value)
        {
        }

        public T GenericBaseMethod2() => default;

        public T2 GenericBaseMethod3<T2>() => default;
    }

    public abstract class GenericAbstractBase<T>
    {
        public abstract T AbstractFunctionInGeneric(T arg);

        public virtual T VirtualFunctionInGeneric(T arg) => arg;

        public T ShadowedGenericFunction(T arg) => arg;
    }

    public class GenericBaseHandler2<T1, T2>
    {
        public void GenericBaseMethod1(T1 value)
        {
        }

        public T1 GenericBaseMethod2() => default;

        public void GenericBaseMethod3(T2 value)
        {
        }

        public T2 GenericBaseMethod4() => default;

        public T2 GenericBaseMethod5(T1 value) => default;

        public void GenericBaseMethod6(T1 val1, T2 val2)
        {
        }

        public Task<T1> GenericBaseMethod7(Task<T2> value) => Task.FromResult<T1>(default);
    }

    public class TrickyParamHandler
    {
        public int GenericArguments(Dictionary<string, string> arg1) => 0;

        public int NestedGenericArguments(NestedGeneric<string, string> arg1) => 0;

        public int RecursiveGenericArguments(Dictionary<string, Dictionary<string, string>> arg1) => 0;

        public int DoublyNestedGenericArguments(NestedClass.InnerGeneric<string, string> arg1) => 0;

        public int NestedRecursiveGenericArguments(NestedGeneric<string, NestedGeneric<string, Dictionary<string, string>>> arg1) => 0;

        public int NestedClassArgument(NestedClass arg1) => 0;

        public int DoublyNestedClassArgument(NestedClass.Inner arg1) => 0;

        public int NestedStructArgument(NestedStruct arg1) => 0;

        public struct NestedStruct
        {
        }

        public class NestedClass
        {
            public class Inner
            {
            }

            public class InnerGeneric<TKey, TValue> : Dictionary<TKey, TValue>
            {
            }
        }

        public class NestedGeneric<TKey, TValue> : Dictionary<TKey, TValue>
        {
            public Task<Tuple<TKey, NestedGeneric<TKey, TValue>>> Handler(Tuple<TKey, NestedGeneric<TKey, TValue>> key) => default;
        }
    }

    public class TestMockSpan : MockSpan
    {
    }

    public class ILambdaContext
    {
    }

    public class DerivedImplementation : GenericBase<CustomInput, string>
    {
        public class NestedDerived : GenericBase<CustomInput, object>
        {
        }
    }

    public class Function
    {
        public class NestedClass
        {
            public class InnerGeneric<TKey, TValue> : Dictionary<TKey, TValue>
            {
            }
        }

        public class NestedGeneric<TKey, TValue> : Dictionary<TKey, TValue>
        {
        }
    }

    public abstract class GenericBase<TRequest, TResponse>
    {
        public GenericBase<CustomInput, Function.NestedClass.InnerGeneric<TRequest, TResponse>> ComplexNestedGeneric(
            TRequest request,
            Function.NestedClass.InnerGeneric<TRequest, NestedInSameType<TRequest, Dictionary<string, Nested.DeepNested<TResponse>>>> context)
        {
            return default;
        }

        public Task NonGenericFunction(string arg1, ILambdaContext arg2) => Task.CompletedTask;

        public class NestedInSameType<TKey, TValue> : Dictionary<TKey, TValue>
        {
        }

        public class Nested
        {
            public class DeepNested<TNested> : HashSet<TNested>
            {
            }
        }
    }

    public class CustomInput
    {
    }
}
