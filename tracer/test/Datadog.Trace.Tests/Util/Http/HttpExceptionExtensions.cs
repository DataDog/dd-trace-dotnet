// <copyright file="HttpExceptionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http;

public class HttpExceptionExtensions
{
    public static TheoryData<Exception> SocketExceptions { get; } = new()
    {
        new SocketException(),
        new WebException("msg", new SocketException()),
#if !NETFRAMEWORK
        new System.Net.Http.HttpRequestException("msg", new SocketException()),
        new WebException("msg", new System.Net.Http.HttpRequestException("msg", new SocketException())),
#endif
    };

    public static TheoryData<Exception> NonSocketExceptions { get; } = new()
    {
        null,
        new WebSocketException(),
        new Exception(),
        new IOException(),
    };

    [Theory]
    [MemberData(nameof(SocketExceptions))]
    public void IsSocketException_True(Exception exception)
    {
        exception.IsSocketException().Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonSocketExceptions))]
    public void IsSocketException_False(Exception exception)
    {
        exception.IsSocketException().Should().BeFalse();
    }
}
