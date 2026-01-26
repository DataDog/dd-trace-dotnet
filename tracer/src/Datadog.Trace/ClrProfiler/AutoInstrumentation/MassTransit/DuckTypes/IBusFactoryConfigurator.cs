// <copyright file="IBusFactoryConfigurator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for MassTransit bus factory configurators
/// (IInMemoryBusFactoryConfigurator, IRabbitMqBusFactoryConfigurator, etc.)
/// Used to navigate the configuration hierarchy to inject filters.
/// </summary>
internal interface IBusFactoryConfigurator
{
    // Note: We don't need specific properties here since we use reflection
    // to navigate the internal configuration structure.
    // This interface serves as a marker for duck-typing.
}
