// <copyright file="BaggageScopeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

[Collection(nameof(BaggageTestCollection))]
[BaggageRestorer]
public class BaggageScopeTests
{
    [Fact]
    public void CaptureMakesTheGivenBaggageCurrent()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var unitOfWork = new Baggage { { "unit", "value" } };
        using var scope = BaggageScope.Capture(unitOfWork);

        Baggage.Current.Should().BeSameAs(unitOfWork);
        Baggage.Current.GetValueOrDefault("previous").Should().BeNull();
    }

    [Fact]
    public void CaptureWithNullStartsFromEmptyWithoutTouchingThePreviousBaggage()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        using var scope = BaggageScope.Capture(null);

        Baggage.Current.Should().NotBeSameAs(previous);
        Baggage.Current.Should().BeEmpty();

        Baggage.Current["unit"] = "value";
        previous.Should().HaveCount(1);
        previous.GetValueOrDefault("unit").Should().BeNull();
    }

    [Fact]
    public void DisposePutsThePreviousBaggageBack()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });
        scope.Dispose();

        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public void DisposePutsTheInstanceBackAfterTheUnitOfWorkMutatedItsOwn()
    {
        var previous = new Baggage();
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });

        Baggage.Current["added"] = "value";
        Baggage.Current.Remove("unit");

        scope.Dispose();

        Baggage.Current.Should().BeSameAs(previous);
        previous.Should().BeEmpty();
    }

    [Fact]
    public void DisposePutsThePreviousBaggageBackEvenAfterTheInstanceWasReplaced()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });

        // Assigning a dictionary through the public API installs a different Baggage instance.
        // Leaving that in place is the leak this type exists to prevent.
        Baggage.Current = new Baggage { { "replacement", "value" } };

        scope.Dispose();

        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public void DisposingTwiceOnOneExecutionContextIsANoOp()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });

        scope.Dispose();
        scope.Dispose();

        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public void NestedScopesDisposedInnermostFirstUnwindToTheOriginalBaggage()
    {
        var previous = new Baggage();
        Baggage.Current = previous;

        var outer = BaggageScope.Capture(new Baggage { { "outer", "value" } });
        var outerBaggage = Baggage.Current;
        var inner = BaggageScope.Capture(new Baggage { { "inner", "value" } });

        inner.Dispose();
        Baggage.Current.Should().BeSameAs(outerBaggage);

        outer.Dispose();
        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public async Task DisposeAppliesOncePerExecutionContext()
    {
        var previous = new Baggage();
        Baggage.Current = previous;

        var unitOfWork = new Baggage { { "unit", "value" } };
        var scope = BaggageScope.Capture(unitOfWork);
        var captured = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var work = Task.Run(async () =>
        {
            await captured.Task;

            // This context captured its own copy of the AsyncLocal slot, so the caller's dispose
            // is not visible here and this context needs its own. An "already disposed" flag on
            // the shared instance would swallow this one and leak the unit of work's baggage.
            Baggage.Current.Should().BeSameAs(unitOfWork);
            scope.Dispose();
            Baggage.Current.Should().BeSameAs(previous);
        });

        scope.Dispose();
        Baggage.Current.Should().BeSameAs(previous);

        captured.SetResult(true);
        await work;
    }
}
