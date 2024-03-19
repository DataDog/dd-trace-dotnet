// <copyright file="ExceptionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.Helpers
{
    internal static class ExceptionExtensions
    {
        public static bool IsSelfOrInnerExceptionEquals(this Exception? checkSelfAndInner, Exception toCheckAgainst, out Exception? matchedException)
        {
            matchedException = checkSelfAndInner;

            if (checkSelfAndInner == null)
            {
                return false;
            }

            return object.ReferenceEquals(checkSelfAndInner, toCheckAgainst) || (checkSelfAndInner is AggregateException == false && IsSelfOrInnerExceptionEquals(checkSelfAndInner.InnerException, toCheckAgainst, out matchedException));
        }
    }
}
