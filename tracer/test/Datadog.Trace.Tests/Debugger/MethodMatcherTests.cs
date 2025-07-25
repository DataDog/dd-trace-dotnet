// <copyright file="MethodMatcherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class MethodMatcherTests
{
    // Regular method tests
    [Fact]
    public void IsMethodMatch_SimpleMethod_MatchesCorrectly()
    {
        // Arrange
        var method = typeof(MethodMatcherTests).GetMethod(
            nameof(IsMethodMatch_SimpleMethod_MatchesCorrectly),
            BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        var stackTraceText = $"Datadog.Trace.Tests.Debugger.MethodMatcherTests.IsMethodMatch_SimpleMethod_MatchesCorrectly";

        // Act
        var result = MethodMatcher.IsMethodMatch(stackTraceText, method);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMethodMatch_MethodWithParameters_MatchesCorrectly()
    {
        // Arrange
        var method = typeof(MethodMatcherTests).GetMethod(
            nameof(TestMethodWithParams),
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        var stackTraceText = "Datadog.Trace.Tests.Debugger.MethodMatcherTests.TestMethodWithParams";

        // Act
        var result = MethodMatcher.IsMethodMatch(stackTraceText, method);

        // Assert
        result.Should().BeTrue();
    }

    // Async method tests
    [Fact]
    public async Task IsMethodMatch_AsyncMethod_MatchesCorrectly()
    {
        // Arrange
        try
        {
            await TestAsyncMethod();  // This will throw
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var stateMachineType = typeof(MethodMatcherTests)
                                  .GetNestedTypes(BindingFlags.NonPublic)
                                  .FirstOrDefault(t => t.Name.Contains(nameof(TestAsyncMethod)) && t.Name.Contains("d__"));

            stateMachineType.Should().NotBeNull("Test setup failed - async state machine type not found");

            var moveNextMethod = stateMachineType.GetMethod(
                "MoveNext",
                BindingFlags.NonPublic | BindingFlags.Instance);
            moveNextMethod.Should().NotBeNull("Test setup failed - MoveNext method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IsMethodMatch_AsyncLambda_MatchesCorrectly()
    {
        // Arrange
        var lambdaField = new Func<Task<int>>(async () =>
        {
            await Task.Delay(1);
            throw new Exception("Test exception");
#pragma warning disable CS0162 // Unreachable code detected
            return 42;
#pragma warning restore CS0162 // Unreachable code detected
        });

        try
        {
            await lambdaField();
        }
        catch (Exception ex)
        {
            // Get the first frame from the stack trace
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var stateMachineType = lambdaField.Method.DeclaringType
                                              .GetNestedTypes(BindingFlags.NonPublic)
                                              .FirstOrDefault(t =>
                                                                  t.Name.Contains("<<IsMethodMatch_AsyncLambda_MatchesCorrectly>b__") &&
                                                                  t.Name.EndsWith(">d"));

            stateMachineType.Should().NotBeNull("Test setup failed - async lambda state machine type not found");

            var moveNextMethod = stateMachineType.GetMethod(
                "MoveNext",
                BindingFlags.NonPublic | BindingFlags.Instance);
            moveNextMethod.Should().NotBeNull("Test setup failed - MoveNext method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    // Iterator tests
    [Fact]
    public void IsMethodMatch_Iterator_MatchesCorrectly()
    {
        // Arrange
        try
        {
            foreach (var item in TestIteratorMethod())
            {
                // Do nothing, just trigger the iterator
            }
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var iteratorType = typeof(MethodMatcherTests)
                .GetNestedTypes(BindingFlags.NonPublic)
                .FirstOrDefault(t => t.GetInterfaces()
                    .Any(i => i.FullName == "System.Collections.IEnumerator"));

            iteratorType.Should().NotBeNull("Test setup failed - iterator type not found");

            var moveNextMethod = iteratorType.GetMethod(
                "MoveNext",
                BindingFlags.NonPublic | BindingFlags.Instance);
            moveNextMethod.Should().NotBeNull("Test setup failed - MoveNext method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    // Nested class tests
    [Fact]
    public void IsMethodMatch_NestedClass_MatchesCorrectly()
    {
        // Arrange
        try
        {
            var nested = new NestedTestClass();
            nested.TestMethod();
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var method = typeof(NestedTestClass).GetMethod(
                nameof(NestedTestClass.TestMethod),
                BindingFlags.Public | BindingFlags.Instance);
            method.Should().NotBeNull("Test setup failed - method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, method);

            // Assert
            result.Should().BeTrue();
        }
    }

    // Generic method tests
    [Fact]
    public void IsMethodMatch_GenericMethod_MatchesCorrectly()
    {
        // Arrange
        var method = typeof(MethodMatcherTests).GetMethod(
            nameof(TestGenericMethod),
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        var stackTraceText = "Datadog.Trace.Tests.Debugger.MethodMatcherTests.TestGenericMethod";

        // Act
        var result = MethodMatcher.IsMethodMatch(stackTraceText, method);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMethodMatch_LocalFunction_MatchesCorrectly()
    {
        try
        {
            TestMethodWithLocalFunction();
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            // Get the exact name from the stack trace
            var method = typeof(MethodMatcherTests).GetMethods(
                                                        BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                                   .FirstOrDefault(m => m.Name.StartsWith("<TestMethodWithLocalFunction>g__LocalFunction|") &&
                                                                        m.Name.EndsWith("_0"));

            method.Should().NotBeNull("Test setup failed - local function method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, method);

            // Assert
            result.Should().BeTrue();
        }
    }

    // Edge cases
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsMethodMatch_InvalidStackTraceText_ReturnsFalse(string stackTraceText)
    {
        // Arrange
        var method = GetType().GetMethod(
            nameof(IsMethodMatch_InvalidStackTraceText_ReturnsFalse),
            BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = MethodMatcher.IsMethodMatch(stackTraceText, method);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsMethodMatch_NullMethodBase_ReturnsFalse()
    {
        // Arrange
        var stackTraceText = "Some.Valid.Method.Name";

        // Act
        var result = MethodMatcher.IsMethodMatch(stackTraceText, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMethodMatch_AsyncDisposable_MatchesCorrectly()
    {
        // Arrange
        try
        {
            await using (var resource = new AsyncDisposableResource())
            {
                await resource.DoWorkAsync();
            }
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var stateMachineType = typeof(AsyncDisposableResource)
                                  .GetNestedTypes(BindingFlags.NonPublic)
                                  .FirstOrDefault(t => t.Name.Contains("DisposeAsync") && t.Name.Contains("d__"));

            stateMachineType.Should().NotBeNull("Test setup failed - async disposable state machine type not found");

            var moveNextMethod = stateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
            moveNextMethod.Should().NotBeNull("Test setup failed - MoveNext method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IsMethodMatch_AsyncIterator_MatchesCorrectly()
    {
        // Arrange
        try
        {
            await foreach (var item in TestAsyncIteratorMethod())
            {
            }
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var stateMachineType = typeof(MethodMatcherTests)
                .GetNestedTypes(BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.Contains(nameof(TestAsyncIteratorMethod)) && t.Name.Contains("d__"));

            stateMachineType.Should().NotBeNull("Test setup failed - async iterator state machine type not found");

            var moveNextMethod = stateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
            moveNextMethod.Should().NotBeNull("Test setup failed - MoveNext method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    [Fact]
    public void IsMethodMatch_AsyncMethodWithGenerics_MatchesCorrectly()
    {
        // Arrange
        try
        {
#pragma warning disable xUnit1031 // Test methods should not use Blocking operations
            AsyncMethodWithGenerics<int, string>(42, "test").GetAwaiter().GetResult();
#pragma warning restore xUnit1031
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var stateMachineType = typeof(MethodMatcherTests)
                .GetNestedTypes(BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.Contains(nameof(AsyncMethodWithGenerics)) && t.Name.Contains("d__"));

            var moveNextMethod = stateMachineType!.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IsMethodMatch_AsyncMethodInGenericClass_MatchesCorrectly()
    {
        // Arrange
        try
        {
            var genericClass = new GenericTestClass<int>();
            await genericClass.AsyncMethod();
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var stateMachineType = typeof(GenericTestClass<>)
                .MakeGenericType(typeof(int))
                .GetNestedTypes(BindingFlags.NonPublic)
                .FirstOrDefault(t => t.Name.Contains("AsyncMethod") && t.Name.Contains("d__"));

            var moveNextMethod = stateMachineType!.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    [Fact]
    public void IsMethodMatch_NestedGenericClass_MatchesCorrectly()
    {
        // Arrange
        try
        {
            var nested = new OuterClass.NestedGeneric<int>();
            nested.ThrowingMethod();
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            var method = typeof(OuterClass.NestedGeneric<>)
                .MakeGenericType(typeof(int))
                .GetMethod(nameof(OuterClass.NestedGeneric<int>.ThrowingMethod));

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, method);

            // Assert
            result.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IsMethodMatch_AsyncLocalFunction_MatchesCorrectly()
    {
        // Arrange
        try
        {
            await TestMethodWithAsyncLocalFunction();
        }
        catch (Exception ex)
        {
            var firstFrame = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0];
            var stackTraceMethodText = firstFrame.Substring(firstFrame.IndexOf("at ") + 3);
            stackTraceMethodText = stackTraceMethodText.Substring(0, stackTraceMethodText.IndexOf(" in "));

            // Look for the async state machine of the local function
            // Pattern is "<<OuterMethod>g__LocalFunctionName|x_y>d"
            var stateMachineType = typeof(MethodMatcherTests)
                                  .GetNestedTypes(BindingFlags.NonPublic)
                                  .FirstOrDefault(t => t.Name.StartsWith("<<TestMethodWithAsyncLocalFunction>g__LocalFunction") &&
                                                       t.Name.EndsWith(">d"));

            stateMachineType.Should().NotBeNull("Test setup failed - async local function state machine type not found");

            var moveNextMethod = stateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
            moveNextMethod.Should().NotBeNull("Test setup failed - MoveNext method not found");

            // Act
            var result = MethodMatcher.IsMethodMatch(stackTraceMethodText, moveNextMethod);

            // Assert
            result.Should().BeTrue();
        }
    }

    // Helper methods used for the tests
    private void TestMethodWithParams(string param1, int param2)
    {
    }

    private async Task TestAsyncMethod()
    {
        await Task.Delay(1);
        throw new Exception("Test exception");
    }

    private IEnumerable<int> TestIteratorMethod()
    {
        yield return 1;
        throw new Exception("Test exception");
#pragma warning disable CS0162 // Unreachable code detected
        yield return 2;
#pragma warning restore CS0162 // Unreachable code detected
    }

    private void TestGenericMethod<T>()
    {
    }

    private void TestMethodWithLocalFunction()
    {
        static void LocalFunction()
        {
            throw new Exception("Test exception from local function");
        }

        LocalFunction();
    }

    private async IAsyncEnumerable<int> TestAsyncIteratorMethod()
    {
        await Task.Delay(1);
        yield return 1;
        throw new Exception("Test exception from async iterator");
    }

    private async Task<TResult> AsyncMethodWithGenerics<T1, TResult>(T1 param1, TResult param2)
    {
        await Task.Delay(1);
        throw new Exception("Test exception from generic async method");
    }

    private async Task TestMethodWithAsyncLocalFunction()
    {
        async Task LocalFunction()
        {
            await Task.Delay(1);
            throw new Exception("Test exception from async local function");
        }

        await LocalFunction();
    }

    private class NestedTestClass
    {
        public void TestMethod()
        {
            throw new Exception("Test exception");
        }
    }

    private class GenericTestClass<T>
    {
        public async Task AsyncMethod()
        {
            await Task.Delay(1);
            throw new Exception("Test exception from generic class async method");
        }
    }

    private class OuterClass
    {
        public class NestedGeneric<T>
        {
            public void ThrowingMethod()
            {
                throw new Exception("Test exception from nested generic class");
            }
        }
    }

    private class AsyncDisposableResource : IAsyncDisposable
    {
        public async Task DoWorkAsync()
        {
            await Task.Delay(1);
            throw new Exception("Test exception from async disposable");
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Delay(1); // Simulate some async cleanup
            throw new Exception("Test exception during async dispose");
        }
    }
}
