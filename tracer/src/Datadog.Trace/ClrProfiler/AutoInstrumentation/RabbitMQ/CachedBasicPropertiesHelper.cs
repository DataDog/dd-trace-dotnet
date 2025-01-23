// <copyright file="CachedBasicPropertiesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

internal static class CachedBasicPropertiesHelper<TBasicProperties>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ActivatorHelper Activator;

    static CachedBasicPropertiesHelper()
    {
        Activator = new ActivatorHelper(typeof(TBasicProperties).Assembly.GetType("RabbitMQ.Client.BasicProperties")!);
    }

    public static TBasicProperties CreateHeaders()
        => (TBasicProperties)Activator.CreateInstance();
}
