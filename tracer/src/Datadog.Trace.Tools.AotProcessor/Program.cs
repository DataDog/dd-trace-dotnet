// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.Extensions.DependencyModel;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.AotProcessor;

internal class Program
{
    public static void Main(string[] args)
    {
        ProfilerInterop.LoadProfiler();
    }
}
