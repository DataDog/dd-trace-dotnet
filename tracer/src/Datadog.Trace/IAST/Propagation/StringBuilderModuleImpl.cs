// <copyright file="StringBuilderModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Iast.Propagation;

internal static class StringBuilderModuleImpl
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringBuilderModuleImpl));

    public static StringBuilder OnStringBuilderInit(StringBuilder builder, string param)
    {
        try
        {
            if (!StringModuleImpl.CanBeTainted(param))
            {
                return builder;
            }

            var ctx = IastModule.GetIastContext();
            if (ctx == null)
            {
                return builder;
            }

            TaintedObjects to = ctx.GetTaintedObjects();
            TaintedObject? paramTainted = to.Get(param);
            if (paramTainted == null)
            {
                return builder;
            }

            to.Taint(builder, paramTainted.Ranges);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringBuilderInit");
        }

        return builder;
    }

    public static string OnStringBuilderToString(object? instance, string result)
    {
        try
        {
            if (instance is StringBuilder target)
            {
                var ctx = IastModule.GetIastContext();
                if (ctx == null)
                {
                    return result;
                }

                TaintedObjects to = ctx.GetTaintedObjects();
                if (to == null)
                {
                    return result;
                }

                var o = to?.Get(target);
                if (o != null)
                {
                    to!.Taint(result, o.Ranges);
                }
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringBuilderInit");
        }

        return result;
    }
}
