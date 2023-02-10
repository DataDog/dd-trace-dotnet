// <copyright file="StringModuleImpl.cs" company="Datadog">
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

internal static class StringModuleImpl
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringModuleImpl));

    internal static bool CanBeTainted(string? txt)
    {
        return txt != null && txt.Length > 0;
    }

    internal static TaintedObject? GetTainted(TaintedObjects to, object? value)
    {
        return value == null ? null : to.Get(value);
    }

    /// <summary> Mostly used overload </summary>
    /// <param name="left"> Param 1 </param>
    /// <param name="right"> Param 2</param>
    /// <param name="result"> Result </param>
    /// <param name="filter"> Literal filter </param>
    /// <returns> resiñt </returns>
    public static string OnStringConcat(string left, string right, string result, AspectFilter filter = AspectFilter.None)
    {
        try
        {
            if (!CanBeTainted(result) || (!CanBeTainted(left) && !CanBeTainted(right)))
            {
                return result;
            }

            var ctx = IastModule.GetIastContext();
            if (ctx == null)
            {
                return result;
            }

            TaintedObjects taintedObjects = ctx.GetTaintedObjects();
            TaintedObject? taintedLeft = filter != AspectFilter.StringLiteral_0 ? GetTainted(taintedObjects, left) : null;
            TaintedObject? taintedRight = filter != AspectFilter.StringLiteral_1 ? GetTainted(taintedObjects, right) : null;
            if (taintedLeft == null && taintedRight == null)
            {
                return result;
            }

            Range[]? ranges;
            if (taintedRight == null)
            {
                ranges = taintedLeft!.Ranges;
            }
            else if (taintedLeft == null)
            {
                ranges = new Range[taintedRight!.Ranges!.Length];
                Ranges.CopyShift(taintedRight.Ranges, ranges, 0, left.Length);
            }
            else
            {
                ranges = Ranges.MergeRanges(left.Length, taintedLeft!.Ranges, taintedRight!.Ranges);
            }

            taintedObjects.Taint(result, ranges);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat");
        }

        return result;
    }

    /// <summary> Overload for multi params (up to 5) </summary>
    /// <param name="parameters"> StringConcat params struct </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringConcat(StringConcatParams parameters, string result)
    {
        try
        {
            if (!CanBeTainted(result) || !parameters.CanBeTainted())
            {
                return result;
            }

            var ctx = IastModule.GetIastContext();
            if (ctx == null)
            {
                return result;
            }

            TaintedObjects to = ctx.GetTaintedObjects();

            Range[]? ranges = null;
            int len = 0;
            for (int x = 0; x < parameters.ParamCount; x++)
            {
                var p = parameters[x];
                if (!CanBeTainted(p))
                {
                    continue;
                }

                var t = GetTainted(to, p);
                if (t != null)
                {
                    if (ranges == null)
                    {
                        if (t != null)
                        {
                            if (len == 0)
                            {
                                ranges = t.Ranges;
                            }
                            else
                            {
                                ranges = new Range[t!.Ranges!.Length];
                                Ranges.CopyShift(t.Ranges, ranges, 0, p!.Length);
                            }
                        }

                        len += p!.Length;
                        continue;
                    }

                    ranges = Ranges.MergeRanges(len, ranges, t.Ranges);
                }

                len += p!.Length;
            }

            if (ranges != null)
            {
                to.Taint(result, ranges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat");
        }

        return result;
    }

    /// <summary> Overload for multi params (up to 5) </summary>
    /// <param name="parameters"> StringConcat params struct </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringConcat(IEnumerable<string> parameters, string result)
    {
        try
        {
            if (!CanBeTainted(result))
            {
                return result;
            }

            var ctx = IastModule.GetIastContext();
            if (ctx == null)
            {
                return result;
            }

            TaintedObjects to = ctx.GetTaintedObjects();

            Range[]? ranges = null;
            int len = 0;
            foreach (var p in parameters)
            {
                if (!CanBeTainted(p))
                {
                    continue;
                }

                var t = GetTainted(to, p);
                if (t != null)
                {
                    if (ranges == null)
                    {
                        if (t != null)
                        {
                            if (len == 0)
                            {
                                ranges = t.Ranges;
                            }
                            else
                            {
                                ranges = new Range[t!.Ranges!.Length];
                                Ranges.CopyShift(t.Ranges, ranges, 0, p.Length);
                            }
                        }

                        len += p!.Length;
                        continue;
                    }

                    ranges = Ranges.MergeRanges(len, ranges, t.Ranges);
                }

                len += p!.Length;
            }

            if (ranges != null)
            {
                to.Taint(result, ranges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat");
        }

        return result;
    }

    /// <summary> Overload for multi params (up to 5) </summary>
    /// <param name="parameters"> StringConcat params struct </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringConcat(IEnumerable<object> parameters, string result)
    {
        try
        {
            if (!CanBeTainted(result))
            {
                return result;
            }

            var ctx = IastModule.GetIastContext();
            if (ctx == null)
            {
                return result;
            }

            TaintedObjects to = ctx.GetTaintedObjects();

            Range[]? ranges = null;
            int len = 0;
            foreach (var po in parameters)
            {
                var p = po?.ToString();
                if (!CanBeTainted(p))
                {
                    continue;
                }

                var t = GetTainted(to, p);
                if (t != null)
                {
                    if (ranges == null)
                    {
                        if (t != null)
                        {
                            if (len == 0)
                            {
                                ranges = t.Ranges;
                            }
                            else
                            {
                                ranges = new Range[t!.Ranges!.Length];
                                Ranges.CopyShift(t.Ranges, ranges, 0, p!.Length);
                            }
                        }

                        len += p!.Length;
                        continue;
                    }

                    ranges = Ranges.MergeRanges(len, ranges, t.Ranges);
                }

                len += p!.Length;
            }

            if (ranges != null)
            {
                to.Taint(result, ranges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat");
        }

        return result;
    }

    public struct StringConcatParams
    {
        public readonly string? P0;
        public readonly string? P1;
        public readonly string? P2;
        public readonly string? P3;
        public readonly string? P4;
        public readonly int ParamCount;

        public StringConcatParams(string? p0, string? p1, string? p2)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            ParamCount = 3;
        }

        public StringConcatParams(string? p0, string? p1, string? p2, string? p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            ParamCount = 4;
        }

        public StringConcatParams(string? p0, string? p1, string? p2, string? p3, string? p4)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            P4 = p4;
            ParamCount = 5;
        }

        public string? this[int index]
        {
            get
            {
                if (index < 0 || index >= ParamCount)
                {
                    throw new IndexOutOfRangeException();
                }

                return index switch
                {
                    0 => P0,
                    1 => P1,
                    2 => P2,
                    3 => P3,
                    4 => P4,
                    _ => null,
                };
            }
        }

        public bool CanBeTainted()
        {
            for (int x = 0; x < ParamCount; x++)
            {
                var p = this[x];
                if (StringModuleImpl.CanBeTainted(this[x]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
