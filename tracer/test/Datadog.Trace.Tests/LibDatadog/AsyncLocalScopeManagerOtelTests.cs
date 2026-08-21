// <copyright file="AsyncLocalScopeManagerOtelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.LibDatadog.OtelThreadContext;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OtelThreadContext;

public class AsyncLocalScopeManagerOtelTests
{
    [Fact]
    public void DisabledPublishersDoNotReceiveScopeChanges()
    {
        var publisher = new RecordingPublisher(isEnabled: false);
        var scopeManager = new AsyncLocalScopeManager(publisher);
        var (rootSpan, _) = CreateSpans();

        var scope = scopeManager.Activate(rootSpan, finishOnClose: false);
        scopeManager.Close(scope);

        publisher.Sets.Should().BeEmpty();
        publisher.ResetCount.Should().Be(0);
    }

    [Fact]
    public void ScopePushPopPublishesOtelContext()
    {
        var publisher = new RecordingPublisher();
        var scopeManager = new AsyncLocalScopeManager(publisher);
        var (rootSpan, childSpan) = CreateSpans();

        var rootScope = scopeManager.Activate(rootSpan, finishOnClose: false);
        var childScope = scopeManager.Activate(childSpan, finishOnClose: false);
        scopeManager.Close(childScope);
        scopeManager.Close(rootScope);

        publisher.Sets.Should().Equal(rootSpan, childSpan, rootSpan);
        publisher.ResetCount.Should().Be(1);
    }

    [Fact]
    public void RawScopeRestorePublishesContext()
    {
        var publisher = new RecordingPublisher();
        var scopeManager = new AsyncLocalScopeManager(publisher);
        var (rootSpan, _) = CreateSpans();
        var rawAccess = (IScopeRawAccess)scopeManager;
        var scope = new Scope(parent: null, span: rootSpan, scopeManager: scopeManager, finishOnClose: false);

        rawAccess.Active = scope;
        rawAccess.Active = null;

        publisher.Sets.Should().ContainSingle().Which.Should().BeSameAs(rootSpan);
        publisher.ResetCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecutionContextTransitionPublishesOnWorkerThread()
    {
        var publisher = new RecordingPublisher();
        var scopeManager = new AsyncLocalScopeManager(publisher);
        var (rootSpan, _) = CreateSpans();
        var rootScope = scopeManager.Activate(rootSpan, finishOnClose: false);
        var workerThreadId = 0;

        await Task.Run(() =>
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            scopeManager.Active.Should().BeSameAs(rootScope);
            publisher.GetCurrentSpan(workerThreadId).Should().BeSameAs(rootSpan);
        });

        scopeManager.Close(rootScope);
    }

    [Fact]
    public void ExecutionContextRestorationClearsSameWorkerThread()
    {
        var publisher = new RecordingPublisher();
        var scopeManager = new AsyncLocalScopeManager(publisher);
        var (rootSpan, _) = CreateSpans();
        var rootScope = scopeManager.Activate(rootSpan, finishOnClose: false);
        var executionContext = ExecutionContext.Capture()
                            ?? throw new InvalidOperationException("Could not capture the active execution context.");
        var workerThreadId = 0;
        Exception? workerException = null;

        var thread = new Thread(() =>
        {
            try
            {
                ExecutionContext.Run(
                    executionContext,
                    _ =>
                    {
                        workerThreadId = Environment.CurrentManagedThreadId;
                        scopeManager.Active.Should().BeSameAs(rootScope);
                        publisher.GetCurrentSpan(workerThreadId).Should().BeSameAs(rootSpan);
                    },
                    state: null);

                publisher.GetCurrentSpan(workerThreadId).Should().BeNull();
                publisher.GetResetCount(workerThreadId).Should().Be(1);
            }
            catch (Exception ex)
            {
                workerException = ex;
            }
        });

        using (ExecutionContext.SuppressFlow())
        {
            thread.Start();
        }

        thread.Join();

        if (workerException is not null)
        {
            throw new InvalidOperationException("The worker thread assertion failed.", workerException);
        }

        scopeManager.Close(rootScope);
    }

    private static (Span RootSpan, Span ChildSpan) CreateSpans()
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        var rootContext = new SpanContext(
            parent: null,
            traceContext: traceContext,
            serviceName: "service",
            traceId: new TraceId(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            spanId: 123);
        var rootSpan = new Span(rootContext, DateTimeOffset.UtcNow);
        traceContext.AddSpan(rootSpan);

        var childContext = new SpanContext(rootContext, traceContext, "service", spanId: 456);
        var childSpan = new Span(childContext, DateTimeOffset.UtcNow);
        traceContext.AddSpan(childSpan);
        return (rootSpan, childSpan);
    }

    private sealed class RecordingPublisher : IOtelThreadContextPublisher
    {
        private readonly Dictionary<int, Span?> _currentSpans = [];
        private readonly Dictionary<int, int> _resetCounts = [];
        private int _resetCount;

        public RecordingPublisher(bool isEnabled = true)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }

        public List<Span> Sets { get; } = [];

        public int ResetCount => Volatile.Read(ref _resetCount);

        public void Set(Span span)
        {
            if (!IsEnabled)
            {
                return;
            }

            lock (_currentSpans)
            {
                Sets.Add(span);
                _currentSpans[Environment.CurrentManagedThreadId] = span;
            }
        }

        public void Reset()
        {
            if (!IsEnabled)
            {
                return;
            }

            var managedThreadId = Environment.CurrentManagedThreadId;
            lock (_currentSpans)
            {
                _currentSpans[managedThreadId] = null;
                _resetCounts[managedThreadId] = GetResetCount(managedThreadId) + 1;
            }

            Interlocked.Increment(ref _resetCount);
        }

        public Span? GetCurrentSpan(int managedThreadId)
        {
            lock (_currentSpans)
            {
                return _currentSpans.TryGetValue(managedThreadId, out var span) ? span : null;
            }
        }

        public int GetResetCount(int managedThreadId)
        {
            lock (_currentSpans)
            {
                return _resetCounts.TryGetValue(managedThreadId, out var count) ? count : 0;
            }
        }
    }
}
