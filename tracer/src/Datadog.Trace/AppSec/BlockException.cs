// <copyright file="BlockException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.AppSec
{
    internal class BlockException : CallTargetBubbleUpException
    {
        // This constructor is not referenced but it's used with reflection
        public BlockException(IResult result)
            : this(result, false)
        {
        }

        public BlockException(IResult result, bool reported = false)
        {
            Result = result;
            Reported = reported;
        }

        public BlockException(IResult result, Dictionary<string, object?> blockInfo, bool reported = false)
            : this(result, reported)
        {
            BlockInfo = blockInfo;
        }

        public IResult Result { get; }

        public bool Reported { get; }

        public Dictionary<string, object?>? BlockInfo { get; }

        internal static BlockException? GetBlockException(Exception? exception)
        {
            while (exception is not null)
            {
                if (exception is BlockException b)
                {
                    return b;
                }

                exception = exception.InnerException;
            }

            return null;
        }
    }
}
