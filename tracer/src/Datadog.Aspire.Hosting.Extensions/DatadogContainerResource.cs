// <copyright file="DatadogContainerResource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Aspire.Hosting.Extensions;

/// <summary>
/// A resource that represents a Datadog Agent container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class DatadogContainerResource(string name) : ContainerResource(name)
{
}
