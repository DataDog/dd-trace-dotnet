// <copyright file="BaggageScopeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

[BaggageRestorer]
public class BaggageScopeTests
{
    [Fact]
    public void CaptureMakesTheGivenBaggageCurrent()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var unitOfWork = new Baggage { { "unit", "value" } };
        BaggageScope.Capture(unitOfWork);

        Baggage.Current.Should().BeSameAs(unitOfWork);
        Baggage.Current.GetValueOrDefault("previous").Should().BeNull();
    }

    [Fact]
    public void CaptureWithNullStartsFromEmptyWithoutTouchingThePreviousBaggage()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        BaggageScope.Capture(null);

        Baggage.Current.Should().NotBeSameAs(previous);
        Baggage.Current.Should().BeEmpty();

        Baggage.Current["unit"] = "value";
        previous.Should().HaveCount(1);
        previous.GetValueOrDefault("unit").Should().BeNull();
    }

    [Fact]
    public void RestorePutsThePreviousBaggageBack()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });
        scope.Restore();

        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public void RestorePutsTheInstanceBackAfterTheUnitOfWorkMutatedItsOwn()
    {
        var previous = new Baggage();
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });

        Baggage.Current["added"] = "value";
        Baggage.Current.Remove("unit");

        scope.Restore();

        Baggage.Current.Should().BeSameAs(previous);
        previous.Should().BeEmpty();
    }

    [Fact]
    public void RestorePutsThePreviousBaggageBackEvenAfterTheInstanceWasReplaced()
    {
        var previous = new Baggage { { "previous", "value" } };
        Baggage.Current = previous;

        var scope = BaggageScope.Capture(new Baggage { { "unit", "value" } });

        // Assigning a dictionary through the public API installs a different Baggage instance.
        // Leaving that in place is the leak this type exists to prevent.
        Baggage.Current = new Baggage { { "replacement", "value" } };

        scope.Restore();

        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public void NestedScopesRestoredInnermostFirstUnwindToTheOriginalBaggage()
    {
        var previous = new Baggage();
        Baggage.Current = previous;

        var outer = BaggageScope.Capture(new Baggage { { "outer", "value" } });
        var outerBaggage = Baggage.Current;
        var inner = BaggageScope.Capture(new Baggage { { "inner", "value" } });

        inner.Restore();
        Baggage.Current.Should().BeSameAs(outerBaggage);

        outer.Restore();
        Baggage.Current.Should().BeSameAs(previous);
    }

    [Fact]
    public async Task RestoreAppliesOncePerExecutionContext()
    {
        var previous = new Baggage();
        Baggage.Current = previous;

        var unitOfWork = new Baggage { { "unit", "value" } };
        var scope = BaggageScope.Capture(unitOfWork);
        var captured = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var work = Task.Run(async () =>
        {
            await captured.Task;

            // This context captured its own copy of the AsyncLocal slot, so the caller's restore
            // is not visible here and this context needs its own.
            Baggage.Current.Should().BeSameAs(unitOfWork);
            scope.Restore();
            Baggage.Current.Should().BeSameAs(previous);
        });

        scope.Restore();
        Baggage.Current.Should().BeSameAs(previous);

        captured.SetResult(true);
        await work;
    }
}
