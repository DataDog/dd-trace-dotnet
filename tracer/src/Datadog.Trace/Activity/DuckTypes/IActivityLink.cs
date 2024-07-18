// <copyright file="IActivityLink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes;

// https://github.com/dotnet/runtime/blob/f2a9ef8d392b72e6f039ec0b87f3eae4307c6cae/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/ActivityLink.cs#L15

internal interface IActivityLink : IDuckType
{
    IActivityContext Context { get; }

    IEnumerable<KeyValuePair<string, object?>>? Tags { get; }
}
