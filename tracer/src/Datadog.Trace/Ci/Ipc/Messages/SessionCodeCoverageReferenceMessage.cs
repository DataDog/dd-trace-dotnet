// <copyright file="SessionCodeCoverageReferenceMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Ipc.Messages;

internal sealed class SessionCodeCoverageReferenceMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCodeCoverageReferenceMessage"/> class for IPC deserialization.
    /// </summary>
    public SessionCodeCoverageReferenceMessage()
    {
        ResultId = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCodeCoverageReferenceMessage"/> class.
    /// </summary>
    /// <param name="source">Coverage source that produced the persisted result.</param>
    /// <param name="resultId">Stable persisted coverage result identity.</param>
    public SessionCodeCoverageReferenceMessage(CodeCoverageReportSource source, string resultId)
    {
        Source = source;
        ResultId = resultId;
    }

    /// <summary>
    /// Gets or sets the coverage source that produced the persisted result.
    /// </summary>
    [JsonProperty("source")]
    public CodeCoverageReportSource Source { get; set; }

    /// <summary>
    /// Gets or sets the stable persisted coverage result identity.
    /// </summary>
    [JsonProperty("result_id")]
    public string ResultId { get; set; }
}
