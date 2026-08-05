// <copyright file="CoreHttpContextStoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Threading;
using Datadog.Trace.AppSec;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Datadog.Trace.Tests.AppSec;

public class CoreHttpContextStoreTests
{
    [Fact]
    public void GivenAContextInTheStore_WhenItIsRemoved_TheStoreIsEmpty()
    {
        var store = new CoreHttpContextStore();
        var context = new DefaultHttpContext();

        store.Set(context);
        store.Get().Should().BeSameAs(context);

        store.Remove();
        store.Get().Should().BeNull();
    }

    /// <summary>
    /// A write to an <see cref="AsyncLocal{T}"/> is only visible to the current ExecutionContext and to the
    /// ones created after it, so anything the application captures mid-request (a Task.Run, a fire and forget
    /// call, an OnCompleted callback...) keeps its own view of the store. Removing the context has to reach
    /// those captures too: ASP.NET Core uninitializes the <see cref="HttpContext"/> and pools it once the
    /// request is over, so a stale reference either throws from inside ASP.NET Core or, worse, ends up reading
    /// another request's data.
    /// </summary>
    [Fact]
    public void GivenAContextCapturedDuringTheRequest_WhenItIsRemoved_TheCaptureSeesNothing()
    {
        var store = new CoreHttpContextStore();
        var context = new DefaultHttpContext();
        store.Set(context);

        // this is what the application does when it queues work while serving the request
        var capturedDuringTheRequest = ExecutionContext.Capture();

        store.Remove();

        store.Get().Should().BeNull();
        ReadStoreIn(store, capturedDuringTheRequest).Should().BeNull();
    }

    [Fact]
    public void GivenACaptureFromAPreviousRequest_WhenANewRequestStarts_TheCaptureDoesNotSeeIt()
    {
        var store = new CoreHttpContextStore();
        var firstRequest = new DefaultHttpContext();
        store.Set(firstRequest);

        var capturedDuringTheFirstRequest = ExecutionContext.Capture();
        store.Remove();

        // the contexts are pooled, so leaking one request's context into a capture from another one would be
        // handing the WAF the wrong request's headers, cookies and body
        store.Set(new DefaultHttpContext());
        ReadStoreIn(store, capturedDuringTheFirstRequest).Should().BeNull();
    }

    private static HttpContext ReadStoreIn(CoreHttpContextStore store, ExecutionContext executionContext)
    {
        // deliberately not null, so that a callback which never ran would fail the assertion instead of passing it
        HttpContext result = new DefaultHttpContext();
        ExecutionContext.Run(executionContext, _ => result = store.Get(), null);
        return result;
    }
}
#endif
