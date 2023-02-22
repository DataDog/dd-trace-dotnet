﻿// <copyright file="EventTrackingSdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class EventTrackingSdkTests
{
    [Fact]
    public void TrackUserLoginSuccessEvent_OnRootSpanDirectly_ShouldSetOnTrace()
    {
        var scopeManager = new AsyncLocalScopeManager();

        var settings = new TracerSettings
        {
            StartupDiagnosticLogEnabled = false
        };
        var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), scopeManager, Mock.Of<IDogStatsd>());

        var rootTestScope = (Scope)tracer.StartActive("test.trace");
        var childTestScope = (Scope)tracer.StartActive("test.trace.child");
        childTestScope.Dispose();

        var id = Guid.NewGuid().ToString();

        EventTrackingSdk.TrackUserLoginSuccessEvent(id, null, tracer);

        var traceContext = rootTestScope.Span.Context.TraceContext;
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.SuccessTrack));
        Assert.Equal(id, traceContext.Tags.GetTag(Tags.User.Id));
    }

    [Fact]
    public void TrackUserLoginSuccessEvent_WithMeta_OnRootSpanDirectly_ShouldSetOnTrace()
    {
        var scopeManager = new AsyncLocalScopeManager();

        var settings = new TracerSettings
        {
            StartupDiagnosticLogEnabled = false
        };
        var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), scopeManager, Mock.Of<IDogStatsd>());

        var rootTestScope = (Scope)tracer.StartActive("test.trace");
        var childTestScope = (Scope)tracer.StartActive("test.trace.child");
        childTestScope.Dispose();

        var id = Guid.NewGuid().ToString();

        var meta = new Dictionary<string, string>()
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        EventTrackingSdk.TrackUserLoginSuccessEvent(id, meta, tracer);

        var traceContext = rootTestScope.Span.Context.TraceContext;
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.SuccessTrack));
        Assert.Equal(id, traceContext.Tags.GetTag(Tags.User.Id));

        foreach (var kvp in meta)
        {
            Assert.Equal(kvp.Value, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.Success + kvp.Key));
        }
    }

    [Fact]
    public void TrackUserLoginFailureEvent_OnRootSpanDirectly_ShouldSetOnTrace()
    {
        var scopeManager = new AsyncLocalScopeManager();

        var settings = new TracerSettings
        {
            StartupDiagnosticLogEnabled = false
        };
        var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), scopeManager, Mock.Of<IDogStatsd>());

        var rootTestScope = (Scope)tracer.StartActive("test.trace");
        var childTestScope = (Scope)tracer.StartActive("test.trace.child");
        childTestScope.Dispose();

        var id = Guid.NewGuid().ToString();

        EventTrackingSdk.TrackUserLoginFailureEvent(id, true, null, tracer);

        var traceContext = rootTestScope.Span.Context.TraceContext;
        Assert.Equal(id, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.FailureUserId));
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.FailureTrack));
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.FailureUserExists));
    }

    [Fact]
    public void TrackUserLoginFailureEvent_WithMeta_OnRootSpanDirectly_ShouldSetOnTrace()
    {
        var scopeManager = new AsyncLocalScopeManager();

        var settings = new TracerSettings
        {
            StartupDiagnosticLogEnabled = false
        };
        var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), scopeManager, Mock.Of<IDogStatsd>());

        var rootTestScope = (Scope)tracer.StartActive("test.trace");
        var childTestScope = (Scope)tracer.StartActive("test.trace.child");
        childTestScope.Dispose();

        var id = Guid.NewGuid().ToString();

        var meta = new Dictionary<string, string>()
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        EventTrackingSdk.TrackUserLoginFailureEvent(id, false, meta, tracer);

        var traceContext = rootTestScope.Span.Context.TraceContext;
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.FailureTrack));
        Assert.Equal(bool.FalseString, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.FailureUserExists));
        Assert.Equal(id, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.FailureUserId));

        foreach (var kvp in meta)
        {
            Assert.Equal(kvp.Value, traceContext.Tags.GetTag(Tags.AppSec.EventsUsersLogin.Failure + kvp.Key));
        }
    }

    [Fact]
    public void TrackCustomEvent_OnRootSpanDirectly_ShouldSetOnTrace()
    {
        var scopeManager = new AsyncLocalScopeManager();

        var settings = new TracerSettings
        {
            StartupDiagnosticLogEnabled = false
        };
        var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), scopeManager, Mock.Of<IDogStatsd>());

        var rootTestScope = (Scope)tracer.StartActive("test.trace");
        var childTestScope = (Scope)tracer.StartActive("test.trace.child");
        childTestScope.Dispose();

        var eventName = Guid.NewGuid().ToString();

        EventTrackingSdk.TrackCustomEvent(eventName, null, tracer);

        var traceContext = rootTestScope.Span.Context.TraceContext;
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.Track(eventName)));
    }

    [Fact]
    public void TrackCustomEvent_WithMeta_OnRootSpanDirectly_ShouldSetOnTrace()
    {
        var scopeManager = new AsyncLocalScopeManager();

        var settings = new TracerSettings { StartupDiagnosticLogEnabled = false };
        var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>(), scopeManager, Mock.Of<IDogStatsd>());

        var rootTestScope = (Scope)tracer.StartActive("test.trace");
        var childTestScope = (Scope)tracer.StartActive("test.trace.child");
        childTestScope.Dispose();

        var meta = new Dictionary<string, string>() { { "key1", "value1" }, { "key2", "value2" } };

        var eventName = Guid.NewGuid().ToString();

        EventTrackingSdk.TrackCustomEvent(eventName, meta, tracer);

        var traceContext = rootTestScope.Span.Context.TraceContext;
        Assert.Equal(bool.TrueString, traceContext.Tags.GetTag(Tags.AppSec.Track(eventName)));

        foreach (var kvp in meta)
        {
            Assert.Equal(kvp.Value, traceContext.Tags.GetTag($"{Tags.AppSec.Events}{eventName}.{kvp.Key}"));
        }
    }
}
