// <copyright file="ConsoleTestsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.IntegrationTests;

[CollectionDefinition(nameof(ConsoleTestsCollection), DisableParallelization = true)]
public class ConsoleTestsCollection
{
}
