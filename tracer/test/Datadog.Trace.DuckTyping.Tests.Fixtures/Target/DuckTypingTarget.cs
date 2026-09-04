// <copyright file="DuckTypingTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping.Tests.Fixtures.Shared;

namespace Datadog.Trace.DuckTyping.Tests.Fixtures.Target;

public sealed class DuckTypingTarget
{
    private readonly FieldValue _field = new();

    public object FieldValue => _field;
}
