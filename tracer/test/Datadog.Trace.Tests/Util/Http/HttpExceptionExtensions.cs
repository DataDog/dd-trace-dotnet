// <copyright file="HttpExceptionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
    public static List<Exception> SocketExceptions { get; } = new()
    {
        new SocketException(),
        new WebException("msg", new SocketException()),
#if !NETFRAMEWORK
        new System.Net.Http.HttpRequestException("msg", new SocketException()),
        new WebException("msg", new System.Net.Http.HttpRequestException("msg", new SocketException())),
#endif
    };

    public static List<Exception> NonSocketExceptions { get; } = new()
    {
        null,
        new WebSocketException(),
        new Exception(),
        new IOException(),
    };

    [Fact]
    public void IsSocketException_True()
    {
        foreach (var exception in SocketExceptions)
        {
            exception.IsSocketException().Should().BeTrue($"{exception.GetType()} should count as SocketException");
        }
    }

    [Fact]
    public void IsSocketException_False()
    {
        foreach (var exception in NonSocketExceptions)
        {
            exception.IsSocketException().Should().BeFalse($"{exception?.GetType()} should not count as SocketException");
        }
    }
}
