// <copyright file="TestSpans.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Sampling;

public static class TestSpans
{
    internal static readonly Span CartCheckoutSpan = new Span(new SpanContext(1, 1, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "checkout", ResourceName = "/api/users/1" };

    internal static readonly Span AddToCartSpan = new Span(new SpanContext(1, 1, serviceName: "shopping-cart-service"), DateTimeOffset.Now) { OperationName = "cart-add", ResourceName = "/api/cart" };

    internal static readonly Span ShippingAuthSpan = new Span(new SpanContext(1, 1, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize", ResourceName = "/api/items/1" };

    internal static readonly Span ShippingRevertSpan = new Span(new SpanContext(1, 1, serviceName: "shipping-auth-service"), DateTimeOffset.Now) { OperationName = "authorize-revert", ResourceName = "/api/users/2" };

    internal static readonly Span RequestShippingSpan = new Span(new SpanContext(1, 1, serviceName: "request-shipping"), DateTimeOffset.Now) { OperationName = "submit", ResourceName = "/api/users/3" };
}
