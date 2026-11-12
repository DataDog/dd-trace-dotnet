// <copyright file="RuntimeAsyncUnobservedTaskExceptionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET10_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

[CollectionDefinition(nameof(RuntimeAsyncUnobservedTaskExceptionTests), DisableParallelization = true)]
[Collection(nameof(RuntimeAsyncUnobservedTaskExceptionTests))]
public class RuntimeAsyncUnobservedTaskExceptionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SyntheticFailureDoesNotRaiseUnobservedTaskException(bool isGeneric, bool useValueTask)
    {
        var exception = new InvalidOperationException("Expected runtime-async failure");
        var unobservedExceptionRaised = false;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
        {
            if (ReferenceEquals(args.Exception.GetBaseException(), exception))
            {
                unobservedExceptionRaised = true;
                args.SetObserved();
            }
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            var taskReference = CreateSyntheticFailure(exception, isGeneric, useValueTask);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            taskReference.IsAlive.Should().BeFalse();
            unobservedExceptionRaised.Should().BeFalse("the runtime propagates the failure through the caller's task");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateSyntheticFailure(Exception exception, bool isGeneric, bool useValueTask)
    {
        Task task;
        if (isGeneric)
        {
            task = useValueTask
                       ? RuntimeAsyncHelper.CreateFailed<int, ValueTask<int>>(exception).AsTask()
                       : RuntimeAsyncHelper.CreateFailed<int, Task<int>>(exception);
        }
        else
        {
            task = useValueTask
                       ? RuntimeAsyncHelper.CreateFailed<ValueTask>(exception).AsTask()
                       : RuntimeAsyncHelper.CreateFailed<Task>(exception);
        }

        return new WeakReference(task);
    }
}

#endif
