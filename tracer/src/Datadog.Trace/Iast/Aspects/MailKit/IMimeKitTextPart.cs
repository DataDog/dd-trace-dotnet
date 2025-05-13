// <copyright file="IMimeKitTextPart.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects;

#nullable enable

internal interface IMimeKitTextPart
{
    string Text { get; set; }

    bool IsHtml { get; }

    void SetText(string charset, string text);

    void SetText(Encoding encoding, string text);
}
