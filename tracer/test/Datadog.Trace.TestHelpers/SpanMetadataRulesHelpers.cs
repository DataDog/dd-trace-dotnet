// <copyright file="SpanMetadataRulesHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.TestHelpers
{
    #pragma warning disable SA1601 // Partial elements should be documented
    public static class SpanMetadataRulesHelpers
    {
        internal static (string PropertyName, string Result) Name(MockSpan span) => (nameof(span.Name), span.Name);

        internal static (string PropertyName, string Result) Type(MockSpan span) => (nameof(span.Type), span.Type);
    }
}
