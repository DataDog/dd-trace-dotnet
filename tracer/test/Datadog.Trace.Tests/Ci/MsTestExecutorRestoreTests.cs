// <copyright file="MsTestExecutorRestoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class MsTestExecutorRestoreTests
{
    [Fact]
    public void SyncExecutorRestoreStateRestoresOriginalExecutorOnMethodEnd()
    {
        var originalExecutor = new object();
        var replacementExecutor = new object();
        var options = new TestMethodOptionsStub { Executor = replacementExecutor };
        var state = TestMethodExecutorRestore.Create(options, originalExecutor, replacementExecutor);

        TestMethodRunnerExecuteTestIntegration.OnMethodEnd<object, object>(new object(), returnValue: null, exception: null, state);

        options.Executor.Should().BeSameAs(originalExecutor);
    }

    [Fact]
    public void AsyncExecutorRestoreStateWaitsForAsyncMethodEnd()
    {
        var originalExecutor = new object();
        var replacementExecutor = new object();
        var testMethodInfo = new TestMethodInfoStub { Executor = replacementExecutor };
        var state = TestMethodExecutorRestore.Create(testMethodInfo, originalExecutor, replacementExecutor);

        TestMethodRunnerExecuteTestIntegrationV3_9.OnMethodEnd<object, object>(new object(), returnValue: null, exception: null, state);
        testMethodInfo.Executor.Should().BeSameAs(replacementExecutor);

        TestMethodRunnerExecuteTestIntegrationV3_9.OnAsyncMethodEnd<object, object>(new object(), returnValue: null, exception: null, state);
        testMethodInfo.Executor.Should().BeSameAs(originalExecutor);
    }

    [Fact]
    public void ExecutorRestoreStateDoesNotOverwriteDifferentExecutor()
    {
        var originalExecutor = new object();
        var replacementExecutor = new object();
        var laterExecutor = new object();
        var testMethodInfo = new TestMethodInfoStub { Executor = laterExecutor };
        var state = TestMethodExecutorRestore.Create(testMethodInfo, originalExecutor, replacementExecutor);

        TestMethodRunnerExecuteTestIntegrationV3_9.OnAsyncMethodEnd<object, object>(new object(), returnValue: null, exception: null, state);

        testMethodInfo.Executor.Should().BeSameAs(laterExecutor);
    }

    private sealed class TestMethodOptionsStub : ITestMethodOptions
    {
        public object? Executor { get; set; }
    }

    private sealed class TestMethodInfoStub : ITestMethodInfoV3_9
    {
        public object? Instance => this;

        public Type Type => typeof(TestMethodInfoStub);

        public string? TestMethodName => nameof(TestMethodInfoStub);

        public string? TestClassName => typeof(MsTestExecutorRestoreTests).FullName;

        public MethodInfo? MethodInfo => typeof(MsTestExecutorRestoreTests).GetMethod(nameof(SyncExecutorRestoreStateRestoresOriginalExecutorOnMethodEnd));

        public object[]? Arguments => null;

        public ITestClassInfo? Parent => null;

        public object? Executor { get; set; }

        public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return nameof(TestMethodInfoStub);
        }
    }
}
