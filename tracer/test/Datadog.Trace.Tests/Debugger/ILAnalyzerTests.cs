// <copyright file="ILAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

public class ILAnalyzerTests
{
    // Known working case - similar to production usage
    [Fact]
    public void HasDirectCallTo_ExceptionDispatchInfoThrow_ReturnsTrue()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.MethodWithExceptionDispatchInfoThrow),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(ExceptionDispatchInfo),
            "Throw");

        // Assert
        result.Should().BeTrue();
    }

    // Regular method tests
    [Fact]
    public void HasDirectCallTo_SimpleMethodWithDirectCall_ReturnsTrue()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.MethodWithThrow),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(Exception),
            "ToString");

        // Assert
        result.Should().BeTrue();
    }

    // Async method test
    [Fact]
    public void HasDirectCallTo_AsyncMethodWithDirectCall_ReturnsTrue()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.AsyncMethodWithThrow),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(Exception),
            "ToString");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasDirectCallTo_MethodWithoutTargetCall_ReturnsFalse()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.MethodWithoutTargetCall),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(Exception),
            "ToString");

        // Assert
        result.Should().BeFalse();
    }

    // Test helper class
    private class TestClass
    {
        public void MethodWithExceptionDispatchInfoThrow()
        {
            try
            {
                throw new Exception("Test");
            }
            catch (Exception ex)
            {
                var edi = ExceptionDispatchInfo.Capture(ex);
                edi.Throw();
            }
        }

        public void MethodWithThrow()
        {
            var ex = new Exception("Test");
            ex.ToString();
        }

        public void MethodWithoutTargetCall()
        {
            var str = "Test";
            str.GetHashCode();
        }

        public async Task AsyncMethodWithThrow()
        {
            await Task.Delay(1);
            var ex = new Exception("Test");
            ex.ToString();
        }
    }
}
