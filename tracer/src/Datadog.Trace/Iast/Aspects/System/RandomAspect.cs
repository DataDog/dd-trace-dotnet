// <copyright file="RandomAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

#nullable enable

/// <summary> Random class aspect </summary>
[AspectClass("mscorlib,System.Runtime.Extensions,System.Runtime", AspectType.Sink, VulnerabilityType.WeakRandomness)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class RandomAspect
{
    private const string _evidenceValue = "System.Random";

    /// <summary>
    /// System.Random aspect method
    /// </summary>
    [AspectMethodInsertAfter("System.Random::.ctor()")]
    [AspectMethodInsertAfter("System.Random::.ctor(System.Int32)")]
    public static void Init()
    {
        try
        {
            IastModule.OnWeakRandomness(_evidenceValue);
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(RandomAspect)}.{nameof(Init)}");
            return;
        }
    }
}
