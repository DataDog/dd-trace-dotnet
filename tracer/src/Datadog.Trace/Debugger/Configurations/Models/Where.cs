// <copyright file="Where.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Configurations.Models;

internal class Where
{
    public string TypeName { get; set; }

    public string MethodName { get; set; }

    public string SourceFile { get; set; }

    public string Signature { get; set; }

    public string[] Lines { get; set; }
}
