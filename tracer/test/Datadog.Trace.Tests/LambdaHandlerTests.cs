// <copyright file="LambdaHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public class LambdaHandlerTests
    {
        private ITestOutputHelper output;

        public LambdaHandlerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        // public LambdaHandlerTests(ITestOutputHelper output)
        // {
        //     var converter = new Converter(output);
        //     Console.SetOut(converter);
        // }

        // private class Converter : TextWriter
        // {
        //     ITestOutputHelper _output;
        //     public Converter(ITestOutputHelper output)
        //     {
        //         _output = output;
        //     }
        //     public override Encoding Encoding
        //     {
        //         get { return Encoding.ASCII; }
        //     }
        //     public override void WriteLine(string message)
        //     {
        //         _output.WriteLine(message);
        //     }
        //     public override void WriteLine(string format, params object[] args)
        //     {
        //         _output.WriteLine(format, args);
        //     }
        //     // public override void Write(char value)
        //     // {
        //     //     throw new NotSupportedException("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
        //     // }
        // }

        [Fact]
        public void ExampleTestName()
        {
            Console.SetOut(new ConsoleWriter(output));
            Assert.True(ToBeTested.Foo());
        }

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
            Console.SetOut(new ConsoleWriter(output));
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be(expectedType);
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Length.Should().Be(2);
            handler.ParamTypeArray[0].Should().Be(ClrNames.Int32);
            handler.ParamTypeArray[1].Should().Be(ClrNames.Int32);
            // Assert.False(ToBeTested.Foo());
        }

        // [Theory(Skip = "We don't currently support open generics in Lambda")]
        [Theory]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod1", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod2", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod3", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod4", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod5", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod6", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod7", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod8", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod9", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod10", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod11", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod12", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod13", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod14", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod15", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod1", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod2", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod3", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod4", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod6", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod7", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod8", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod1")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod9", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod2")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod10", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod11", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod12", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.FunctionImplementation::FunctionHandlerAsync", "Datadog.Trace.Tests.AbstractAspNetCoreFunction`2", "FunctionHandlerAsync", "Task[!1]", "!0", "Amazon.Lambda.Core.ILambdaContext")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod14", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        // [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler5::GenericBaseMethod15", "Datadog.Trace.Tests.GenericBaseHandler`1", "GenericBaseMethod3")]
        public void LambdaHandlerCanHandleOpenGenericTypes(string handlerVariable, string expectedType, string expectedMethod, params string[] args)
        {
            Console.SetOut(new ConsoleWriter(output));
            LambdaHandler handler = new LambdaHandler(handlerVariable);
            handler.Assembly.Should().Be("Datadog.Trace.Tests");
            handler.FullType.Should().Be(expectedType);
            handler.MethodName.Should().Be(expectedMethod);
            handler.ParamTypeArray.Should().BeSameAs(args);
            // Assert.False(ToBeTested.Foo());
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
            Assert.Throws<Exception>(() => new LambdaHandler(handlerVariable));
        }

        [Theory]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod1")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod2")]
        [InlineData("Datadog.Trace.Tests::Datadog.Trace.Tests.TestHandler4::GenericBaseMethod3")]
        public void LambdaHandlerThrowsWhenInstrumentingGenericTypes(string handlerVariable)
        {
            // We don't support this yet (we could, we just haven't done the work for it yet)
            Assert.Throws<Exception>(() => new LambdaHandler(handlerVariable));
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

        public class ConsoleWriter : StringWriter
        {
            private ITestOutputHelper output;

            public ConsoleWriter(ITestOutputHelper output)
            {
                this.output = output;
            }

            public override void WriteLine(string m)
            {
                output.WriteLine("HARV DEBUG LINE: " + m);
            }
        }

        public class ToBeTested
        {
            public static bool Foo()
            {
                Console.WriteLine("Foo uses Console.WriteLine!!!");
                return true;
            }
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

    public class TestHandler5 : GenericBaseHandler2<int, string>
    {
    }

    // Let's see if this works
    public class TestHandler6 : GenericBaseHandler2<GenericBaseHandler<GenericBaseHandler<TrickyParamHandler>>, GenericBaseHandler2<GenericBaseHandler<TrickyParamHandler>, TestMockSpan>>
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

        public void GenericBaseMethod4<T2>()
        {
        }

        public void GenericBaseMethod5<T2, T3>()
        {
        }

        public void GenericBaseMethod6<T3, T2>()
        {
        }

        public T GenericBaseMethod7() => default;

        public T GenericBaseMethod8<T2>() => default;

        public T GenericBaseMethod9<T3, T2>() => default;

        public T GenericBaseMethod10<T2>() => default;

        public T2 GenericBaseMethod11<T3, T2>() => default;

        public void GenericBaseMethod12<T2>(T2 val1, T2 val3)
        {
        }

        public T2 GenericBaseMethod13<T2>(T2 val1) => default;

        public T GenericBaseMethod14<T2>(T2 val1, T val3) => default;

        public T3 GenericBaseMethod15<T3>(T val1, T3 val3) => default;
    }

    public class GenericBaseHandler2<T, T2>
    {
        public void GenericBaseMethod1(T value)
        {
        }

        public T GenericBaseMethod2() => default;

        public T2 GenericBaseMethod3<T3>() => default;

        public void GenericBaseMethod4<T3>()
        {
        }

        public void GenericBaseMethod5<T3, T4>()
        {
        }

        public void GenericBaseMethod6<T4, T3>()
        {
        }

        public T GenericBaseMethod7() => default;

        public T GenericBaseMethod8<T3>() => default;

        public T GenericBaseMethod9<T3, T4>() => default;

        public T2 GenericBaseMethod10<T3>() => default;

        public T2 GenericBaseMethod11<T3, T4>() => default;

        public void GenericBaseMethod12<T3>(T2 val1, T val3)
        {
        }

        public T2 GenericBaseMethod13<T3>(T val1, T2 val3) => default;

        public T3 GenericBaseMethod14<T3>(T2 val1, T val3) => default;

        public T3 GenericBaseMethod15<T3>(T2 val1, T3 val3) => default;
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
        }
    }

    public class TestMockSpan : MockSpan
    {
    }

    public class ILambdaContext
    {
    }
}
