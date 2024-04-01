// <copyright file="TestSpans.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Sampling;

public static class TestSpans
{
    internal static readonly Span CartCheckoutSpan;

    internal static readonly Span AddToCartSpan;

    internal static readonly Span ShippingAuthSpan;

    internal static readonly Span ShippingRevertSpan;

    internal static readonly Span RequestShippingSpan;

    static TestSpans()
    {
        var now = DateTimeOffset.Now;

        CartCheckoutSpan = new Span(new SpanContext(1, 1, serviceName: "shopping-cart-service"), now) { OperationName = "checkout", ResourceName = "/api/users/1" };
        CartCheckoutSpan.SetTag("tag1", "value1");
        CartCheckoutSpan.SetMetric("tag2", 401);

        AddToCartSpan = new Span(new SpanContext(1, 1, serviceName: "shopping-cart-service"), now) { OperationName = "cart-add", ResourceName = "/api/cart" };
        AddToCartSpan.SetTag("tag1", "value2");
        AddToCartSpan.SetMetric("tag2", 402);

        ShippingAuthSpan = new Span(new SpanContext(1, 1, serviceName: "shipping-auth-service"), now) { OperationName = "authorize", ResourceName = "/api/items/1" };
        ShippingAuthSpan.SetTag("tag1", "value3");
        ShippingAuthSpan.SetMetric("tag2", 410);

        ShippingRevertSpan = new Span(new SpanContext(1, 1, serviceName: "shipping-auth-service"), now) { OperationName = "authorize-revert", ResourceName = "/api/users/2" };
        ShippingRevertSpan.SetTag("tag1", "value4");

        RequestShippingSpan = new Span(new SpanContext(1, 1, serviceName: "request-shipping"), now) { OperationName = "submit", ResourceName = "/api/users/3" };
        RequestShippingSpan.SetMetric("tag2", 403);
    }
}
