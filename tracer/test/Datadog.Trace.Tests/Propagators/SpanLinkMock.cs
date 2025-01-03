// <copyright file="SpanLinkMock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Tests.Propagators;

internal class SpanLinkMock
{
    public List<KeyValuePair<string, string>> Attributes { get; set; }

    public SpanContextMock Context { get; set; }
}
