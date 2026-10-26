// <copyright file="KafkaBaggageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Kafka;

public class KafkaBaggageTests
{
    [Theory]
    [InlineData(null, false, null)]
    [InlineData(null, true, null)]
    [InlineData("second=message", true, "message")]
    public async Task SequentialMessagesDoNotShareBaggage(string secondHeader, bool hasHeaders, string expectedSecond)
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage { { "ambient", "original" }, { "shared", "original" } };
        Baggage.Current = ambientBaggage;

        using var first = CreateConsumerScope(tracer, "first=message,shared=overridden");
        first.Should().NotBeNull();
        Baggage.Current.GetValueOrDefault("first").Should().Be("message");
        Baggage.Current.GetValueOrDefault("shared").Should().Be("overridden");

        // A message sees only its own baggage; the consumer's ambient baggage is not inherited.
        Baggage.Current.GetValueOrDefault("ambient").Should().BeNull();
        Baggage.Current["handler"] = "first";

        // Consumer.Consume closes the previous processing scope before polling again.
        KafkaHelper.CloseConsumerScope();

        Baggage.Current.Should().BeSameAs(ambientBaggage);
        ambientBaggage.Should().HaveCount(2);
        ambientBaggage.GetValueOrDefault("shared").Should().Be("original");

        using var second = CreateConsumerScope(tracer, secondHeader, hasHeaders);
        second.Should().NotBeNull();
        Baggage.Current.GetValueOrDefault("first").Should().BeNull();
        Baggage.Current.GetValueOrDefault("handler").Should().BeNull();
        Baggage.Current.GetValueOrDefault("ambient").Should().BeNull();
        Baggage.Current.GetValueOrDefault("shared").Should().BeNull();
        Baggage.Current.GetValueOrDefault("second").Should().Be(expectedSecond);

        KafkaHelper.CloseConsumerScope();
        Baggage.Current.Should().BeSameAs(ambientBaggage);
    }

    [Fact]
    public async Task ClosingConsumerScopeRestoresBaggage()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage { { "ambient", "original" } };
        Baggage.Current = ambientBaggage;

        using var scope = CreateConsumerScope(tracer, "message=value");
        scope.Should().NotBeNull();
        Baggage.Current.GetValueOrDefault("message").Should().Be("value");

        KafkaHelper.CloseConsumerScope();
        Baggage.Current.Should().BeSameAs(ambientBaggage);
        Baggage.Current.GetValueOrDefault("message").Should().BeNull();

        // Once cleanup is complete, another call must leave unrelated work alone.
        using var unrelatedScope = tracer.StartActiveInternal("unrelated");
        var unrelatedBaggage = new Baggage();
        Baggage.Current = unrelatedBaggage;
        KafkaHelper.CloseConsumerScope();
        tracer.InternalActiveScope.Should().BeSameAs(unrelatedScope);
        Baggage.Current.Should().BeSameAs(unrelatedBaggage);
    }

    [Fact]
    public async Task ClosingConsumerScopeFinishesTheSpanEvenWhenProcessingLeftAScopeActive()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        using var scope = CreateConsumerScope(tracer, "message=value");
        scope.Should().NotBeNull();

        // Message processing opened a scope of its own and never disposed it, so the consumer
        // scope is no longer the active one when Consume() is called again.
        using var leaked = tracer.StartActiveInternal("processing");

        KafkaHelper.CloseConsumerScope();

        // The message is over, so its span is finished and its baggage put back rather than being
        // stranded until the execution context goes away.
        scope.Span.IsFinished.Should().BeTrue();
        Baggage.Current.Should().BeSameAs(ambientBaggage);

        // The scope manager declines to deactivate a scope that is not the active one, so the
        // application's own scope is left exactly where it was.
        tracer.InternalActiveScope.Should().BeSameAs(leaked);
    }

    [Fact]
    public async Task ClosingConsumerScopeRestoresBaggageTheMessageReplacedRatherThanMutated()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        using var scope = CreateConsumerScope(tracer, "message=value");
        scope.Should().NotBeNull();

        // Handlers can install a different Baggage instance rather than mutating the one they were
        // given — assigning Baggage.Current a dictionary does exactly that.
        Baggage.Current = new Baggage { { "replacement", "value" } };

        KafkaHelper.CloseConsumerScope();

        // Leaving the replacement current would carry it into whatever the consumer does next.
        Baggage.Current.Should().BeSameAs(ambientBaggage);
    }

    [Fact]
    public async Task ProcessingThatLeavesAScopeActiveDoesNotAccumulateTrackedScopes()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage { { "ambient", "original" } };
        Baggage.Current = ambientBaggage;

        // Message processing opens a scope and never disposes it, so the consumer scope is not the
        // active scope when Consume() is called again. Consume() closes before it creates, so the
        // loop still pops exactly what it pushed however long it runs: an entry that accumulated in
        // one iteration is already visible as the wrong baggage at the top of the next.
        for (var i = 0; i < 3; i++)
        {
            KafkaHelper.CloseConsumerScope();

            // Landing anywhere but the consumer's own baggage would mean an entry accumulated.
            Baggage.Current.Should().BeSameAs(ambientBaggage);

            CreateConsumerScope(tracer, $"message={i}").Should().NotBeNull();
            Baggage.Current.GetValueOrDefault("message").Should().Be(i.ToString());

            tracer.StartActiveInternal($"processing-{i}");
        }

        KafkaHelper.CloseConsumerScope();
        Baggage.Current.Should().BeSameAs(ambientBaggage);
        Baggage.Current.GetValueOrDefault("ambient").Should().Be("original");
    }

    [Fact]
    public async Task ConsumerScopeClosedByTheApplicationIsStillPoppedOnTheNextPoll()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        var first = CreateConsumerScope(tracer, "first=message");
        first.Should().NotBeNull();

        // Application code closed the scope itself, so the span is already over by the time
        // CloseConsumerScope runs for this message.
        first.Dispose();

        KafkaHelper.CloseConsumerScope();
        Baggage.Current.Should().BeSameAs(ambientBaggage);

        // The next message starts from the consumer's baggage rather than the first message's.
        using var second = CreateConsumerScope(tracer, "second=message");
        second.Should().NotBeNull();
        Baggage.Current.GetValueOrDefault("first").Should().BeNull();
        Baggage.Current.GetValueOrDefault("second").Should().Be("message");

        KafkaHelper.CloseConsumerScope();
        Baggage.Current.Should().BeSameAs(ambientBaggage);
    }

    [Fact]
    public async Task MessageWithoutHeadersDoesNotMutateAmbientBaggage()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        using var scope = CreateConsumerScope(tracer, header: null, hasHeaders: false);
        scope.Should().NotBeNull();
        Baggage.Current["handler"] = "value";

        KafkaHelper.CloseConsumerScope();

        Baggage.Current.Should().BeSameAs(ambientBaggage);
        ambientBaggage.Should().BeEmpty();
    }

    [Fact]
    public async Task ClosingConsumerScopeDoesNotClearBaggageInCapturedExecutionContext()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var scope = CreateConsumerScope(tracer, "message=value");
        scope.Should().NotBeNull();
        var messageBaggage = Baggage.Current;
        var processing = Task.Run(async () =>
        {
            await closed.Task;
            Baggage.Current.Should().BeSameAs(messageBaggage);
            Baggage.Current.GetValueOrDefault("message").Should().Be("value");

            // The same scope can be closed independently in each execution context.
            KafkaHelper.CloseConsumerScope();
            Baggage.Current.Should().BeSameAs(ambientBaggage);
        });

        KafkaHelper.CloseConsumerScope();
        closed.SetResult(true);
        await processing;

        Baggage.Current.Should().BeSameAs(ambientBaggage);
        ambientBaggage.Should().BeEmpty();
    }

    [Fact]
    public async Task ImmediateCloseRecordsExceptionAndKeepsMessageBaggage()
    {
        await using var tracer = CreateTracer(createConsumerScopeEnabled: false);
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        var scope = CreateConsumerScope(tracer, "message=value");
        scope.Should().NotBeNull();

        scope.DisposeWithException(new InvalidOperationException("Consume failed"));

        scope.Span.Error.Should().BeTrue();
        tracer.InternalActiveScope.Should().BeNull();

        // The application processes the message after Consume() returns, so the baggage it was
        // produced with has to outlive the span.
        Baggage.Current.GetValueOrDefault("message").Should().Be("value");

        // The next poll still puts the consumer's own baggage back, so it does not leak onward.
        KafkaHelper.CloseConsumerScope();
        Baggage.Current.Should().BeSameAs(ambientBaggage);
        ambientBaggage.Should().BeEmpty();
    }

    [Fact]
    public async Task ImmediateCloseKeepsMessageBaggageOutOfTheNextMessage()
    {
        await using var tracer = CreateTracer(createConsumerScopeEnabled: false);
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        CreateConsumerScope(tracer, "first=message").DisposeWithException(null);
        Baggage.Current.GetValueOrDefault("first").Should().Be("message");

        KafkaHelper.CloseConsumerScope();
        CreateConsumerScope(tracer, "second=message").DisposeWithException(null);

        Baggage.Current.GetValueOrDefault("second").Should().Be("message");
        Baggage.Current.GetValueOrDefault("first").Should().BeNull();

        KafkaHelper.CloseConsumerScope();
    }

    [Fact]
    public async Task NestedConsumerScopesRestoreTheirOwnBaggage()
    {
        await using var tracer = CreateTracer();
        var ambientBaggage = new Baggage();
        Baggage.Current = ambientBaggage;

        using var outer = CreateConsumerScope(tracer, "outer=value");
        outer.Should().NotBeNull();
        var outerBaggage = Baggage.Current;

        using (tracer.StartActiveInternal("child"))
        {
            using var inner = CreateConsumerScope(tracer, "inner=value");
            inner.Should().NotBeNull();
            Baggage.Current.GetValueOrDefault("inner").Should().Be("value");

            KafkaHelper.CloseConsumerScope();

            Baggage.Current.Should().BeSameAs(outerBaggage);
            Baggage.Current.GetValueOrDefault("outer").Should().Be("value");
            Baggage.Current.GetValueOrDefault("inner").Should().BeNull();
        }

        KafkaHelper.CloseConsumerScope();

        Baggage.Current.Should().BeSameAs(ambientBaggage);
        ambientBaggage.Should().BeEmpty();
    }

    private static ScopedTracer CreateTracer(bool createConsumerScopeEnabled = true)
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.PropagationStyleExtract, "baggage" },
            { ConfigurationKeys.KafkaCreateConsumerScopeEnabled, createConsumerScopeEnabled.ToString() },
        }));
        return TracerHelper.CreateWithFakeAgent(settings);
    }

    private static Scope CreateConsumerScope(Tracer tracer, string header, bool hasHeaders = true)
    {
        var headers = new Mock<IHeaders>();
        if (header is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(header);
            headers.Setup(h => h.TryGetLastBytes("baggage", out bytes)).Returns(true);
        }

        var message = new Mock<IMessage>();
        message.Setup(m => m.Instance).Returns(new object());
        message.Setup(m => m.Timestamp).Returns(Mock.Of<ITimestamp>());
        message.Setup(m => m.Value).Returns("message");
        message.Setup(m => m.Headers).Returns(hasHeaders ? headers.Object : null);

        return KafkaHelper.CreateConsumerScope(tracer, tracer.TracerManager.DataStreamsManager, new object(), "topic", null, null, message.Object);
    }
}
