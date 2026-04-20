// <copyright file="TestCoverage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Tests;

/// <summary>
/// Test Coverage Payload
/// </summary>
internal sealed class TestCoverage : IEvent
{
    /// <summary>
    /// Gets or sets the test session unique identifier.
    /// </summary>
    [JsonProperty("test_session_id")]
    public ulong SessionId { get; set; }

    /// <summary>
    /// Gets or sets the test suite unique identifier.
    /// </summary>
    [JsonProperty("test_suite_id")]
    public ulong SuiteId { get; set; }

    /// <summary>
    /// Gets or sets the span's unique identifier.
    /// </summary>
    [JsonProperty("span_id")]
    public ulong SpanId { get; set; }

    /// <summary>
    /// Gets or sets the files with coverage information
    /// </summary>
    [JsonProperty("files")]
    public List<FileCoverage>? Files { get; set; }
}
