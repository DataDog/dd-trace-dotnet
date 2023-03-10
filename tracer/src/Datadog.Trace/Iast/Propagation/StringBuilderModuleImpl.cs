// <copyright file="StringBuilderModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Propagation;

internal static class StringBuilderModuleImpl
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringBuilderModuleImpl));

    public static StringBuilder OnStringBuilderAppend(StringBuilder builder, int initialBuilderLength, object? appendValue, int appendValueLength, int startIndex, int count)
    {
        try
        {
            if (appendValue == null)
            {
                return builder;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return builder;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var paramTainted = taintedObjects.Get(appendValue);

            if (paramTainted == null)
            {
                return builder;
            }

            bool appendWholeParameter = appendValueLength == count && startIndex == 0;

            Range[]? paramRanges;
            if (appendWholeParameter)
            {
                paramRanges = paramTainted.Ranges;
            }
            else
            {
                paramRanges = Ranges.ForSubstring(startIndex, count, paramTainted.Ranges);
            }

            if (paramRanges == null)
            {
                return builder;
            }

            var builderTainted = taintedObjects.Get(builder);

            if (builderTainted == null)
            {
                var ranges = new Range[paramRanges.Length];
                Ranges.CopyShift(paramRanges, ranges, 0, initialBuilderLength);
                taintedObjects.Taint(builder, ranges);
            }
            else
            {
                var builderRanges = builderTainted.Ranges;
                builderTainted.Ranges = Ranges.MergeRanges(initialBuilderLength, builderRanges, paramRanges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.onStringBuilderAppend(StringBuilder,string) exception");
        }

        return builder;
    }

    public static StringBuilder? OnStringBuilderSubSequence(string originalString, int beginIndex, int length, StringBuilder result)
    {
        try
        {
            if (result == null || string.IsNullOrEmpty(originalString))
            {
                return result;
            }

            PropagationModuleImpl.OnStringSubSequence(originalString, beginIndex, result, length);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringBuilderSubSequence(string,int,int,string) exception");
        }

        return result;
    }
}
