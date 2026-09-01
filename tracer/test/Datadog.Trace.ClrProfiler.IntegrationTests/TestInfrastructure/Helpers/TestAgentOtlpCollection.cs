// <copyright file="TestAgentOtlpCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Serializes every test class that reads from the shared ddapm test-agent OTLP session.
    /// Those tests call /test/session/clear, which wipes the session for everyone, so they must
    /// not run concurrently with each other.
    /// </summary>
    [CollectionDefinition(nameof(TestAgentOtlpCollection), DisableParallelization = true)]
    public class TestAgentOtlpCollection
    {
    }
}
