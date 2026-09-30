// <copyright file="DelegateInstrumentationUnobservedTaskExceptionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Util.Delegates;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Delegates;

[CollectionDefinition(nameof(DelegateInstrumentationUnobservedTaskExceptionTests), DisableParallelization = true)]
[Collection(nameof(DelegateInstrumentationUnobservedTaskExceptionTests))]
public class DelegateInstrumentationUnobservedTaskExceptionTests
{
    [Fact]
    public void SynchronouslyThrownExceptionDoesNotCreateUnobservedTaskException()
    {
        var expectedException = new InvalidOperationException("Expected");
        var unobservedExceptionRaised = false;

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
        {
            if (ReferenceEquals(args.Exception.GetBaseException(), expectedException))
            {
                unobservedExceptionRaised = true;
                args.SetObserved();
            }
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            Func<Task<int>> throwingDelegate = () => throw expectedException;
            var wrappedDelegate = DelegateInstrumentation.Wrap(throwingDelegate, new DelegateFunc0Callbacks());

            var exception = Assert.Throws<InvalidOperationException>(() => { _ = wrappedDelegate(); });
            exception.Should().BeSameAs(expectedException);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            unobservedExceptionRaised.Should().BeFalse(
                "a synchronously propagated exception must not also fault an orphaned continuation task");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }
}
