// <copyright file="TraceSources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    /// <summary>
    /// Trace source products (to be used in the '_dd.p.ts' tag)
    /// </summary>
    [Flags]
    [EnumExtensions]
    internal enum TraceSources : byte
    {
        None = 0,
        Apm = 1 << 0,
        Asm = 1 << 1,
        Dsm = 1 << 2,
        Djm = 1 << 3,
        Dbm = 1 << 4,
    }
}
