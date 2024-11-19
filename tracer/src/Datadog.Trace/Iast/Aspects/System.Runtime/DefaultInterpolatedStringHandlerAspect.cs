// <copyright file="DefaultInterpolatedStringHandlerAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.Runtime;

#pragma warning disable DD0005
#pragma warning disable SA1642
#pragma warning disable SA1107

/// <summary> DefaultInterpolatedString class aspect </summary>
[AspectClass("System.Runtime")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class DefaultInterpolatedStringHandlerAspect
{
    /// <summary>
    /// System.Reflection Assembly.Load aspects
    /// </summary>
    /// <param name="target"> target </param>
    /// <param name="value"> value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(System.String)", 0)]
    public static void AppendFormatted(ref DefaultInterpolatedStringHandler target, string value)
    {
        target.AppendFormatted(value);

        try
        {
            Console.WriteLine("AppendFormatted: " + value);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DefaultInterpolatedStringHandlerAspect)}.{nameof(AppendFormatted)}");
        }
    }

    /// <summary> ctor aspect </summary>
    /// <param name="target"> Init target </param>
    /// <param name="value"> Init string </param>
    /// <param name="value2"> Init string2 </param>
    [AspectCtorReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor(System.Int32,System.Int32)")]
    public static void Init(ref InterpolatedStringHandlerWrapper target, int value, int value2)
    {
        // Crashing the process
        target = new InterpolatedStringHandlerWrapper();
    }

    /// <summary> InterpolatedStringHandlerWrapper ctor aspect </summary>
    public ref struct InterpolatedStringHandlerWrapper
    {
        private DefaultInterpolatedStringHandler _handler;

        /// <summary> Test </summary>
        public Span<char> Test;

        /// <summary>
        /// Initializes a new instance of the <see cref="InterpolatedStringHandlerWrapper"/> struct.
        /// </summary>
        /// <param name="handler"> h </param>
        public InterpolatedStringHandlerWrapper(DefaultInterpolatedStringHandler handler)
        {
            _handler = handler;
            Test = new Span<char>("lol".ToCharArray());
        }

        /// <summary> AppendFormatted </summary>
        /// <param name="value"> value </param>
        public void AppendFormatted(string value)
        {
            _handler.AppendFormatted(value);
        }
    }
}

#endif
