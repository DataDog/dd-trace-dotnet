// <copyright file="IMqMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

internal interface IMqMessage
{
    public int MessageLength { get;  }

    [DuckField(Name = "properties")]
    public Hashtable Properties { get; }

    public void SetStringProperty(string name, string value);

    public string GetStringProperty(string name);

    public void WriteBytes(string s);

    public void DeleteProperty(string name);
}
