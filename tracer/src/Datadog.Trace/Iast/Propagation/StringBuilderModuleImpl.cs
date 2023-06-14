// <copyright file="StringBuilderModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;

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
                builderTainted.Ranges = Ranges.MergeRanges(initialBuilderLength, builderTainted.Ranges, paramRanges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringBuilderModuleImpl.OnStringBuilderAppend(StringBuilder, int, object, int, int, int) exception");
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
            Log.Error(err, "StringBuilderModuleImpl.OnStringBuilderSubSequence(string,int,int,StringBuilder) exception");
        }

        return result;
    }

    public static StringBuilder TaintFullStringBuilderIfTainted(StringBuilder target, object? argument1 = null)
    {
        try
        {
            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return target;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var tainted = taintedObjects?.Get(target);

            if (tainted == null)
            {
                return target;
            }

            if (tainted?.Ranges?.Length > 0)
            {
                var source = tainted.Ranges[0].Source;

                if (source is not null)
                {
                    tainted.Ranges = new Range[] { new Range(0, target.Length, source) };
                }
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "PropagationModuleImpl.TaintFullStringBuilderIfTainted exception");
        }

        return target;
    }

    /// <summary> Taints a string.Insert operation </summary>
    /// <param name="target"> original string </param>
    /// <param name="previousLength"> previous length of the string builder </param>
    /// <param name="index"> start index </param>
    /// <param name="valueToInsert"> value to insert </param>
    /// <param name="valueToInsertRepetitions"> times to insert </param>
    /// <param name="valueToInsertIndex"> index of the first char to insert </param>
    /// <param name="valueToInsertCharCount"> chars to insert </param>
    /// <returns> result </returns>
    public static StringBuilder OnStringBuilderInsert(StringBuilder target, int previousLength, int index, object? valueToInsert, int valueToInsertRepetitions = 1, int valueToInsertIndex = 0, int valueToInsertCharCount = -1)
    {
        try
        {
            if (valueToInsert is null)
            {
                return target;
            }

            var valueLenght = target.Length - previousLength;

            if (valueLenght == 0)
            {
                return target;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return target;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedTarget = PropagationModuleImpl.GetTainted(taintedObjects, target);
            var taintedValue = PropagationModuleImpl.GetTainted(taintedObjects, valueToInsert);

            if (taintedValue == null && taintedTarget == null)
            {
                return target;
            }

            Range[]? valueToInsertRanges;

            if (valueToInsertCharCount > 0 && valueToInsert is char[] valueToInsertArray)
            {
                valueToInsertRanges = GetSubRange(valueToInsertIndex, valueToInsertCharCount, taintedValue);
            }
            else
            {
                valueToInsertRanges = GetRepeatedRange(valueToInsertRepetitions, valueLenght, taintedValue);
            }

            var newRangesLeft = taintedTarget != null ? Ranges.ForRemove(index, target.Length, taintedTarget.Ranges) : null;
            var newRangesRight = taintedTarget != null ? Ranges.ForRemove(0, index, taintedTarget.Ranges) : null;
            var rangesTotal = (newRangesLeft?.Length ?? 0) + (valueToInsertRanges?.Length ?? 0) + (newRangesRight?.Length ?? 0);
            var rangesResult = new Range[rangesTotal];

            if (newRangesLeft != null)
            {
                Ranges.CopyShift(newRangesLeft, rangesResult, 0, 0);
            }

            if (valueToInsertRanges != null)
            {
                Ranges.CopyShift(valueToInsertRanges, rangesResult, (newRangesLeft?.Length ?? 0), index);
            }

            if (newRangesRight != null)
            {
                Ranges.CopyShift(newRangesRight, rangesResult, (newRangesLeft?.Length ?? 0) + (valueToInsertRanges?.Length ?? 0), index + valueLenght);
            }

            if (taintedTarget is null)
            {
                taintedObjects.Taint(target, rangesResult);
            }
            else
            {
                taintedTarget.Ranges = rangesResult;
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringBuilderInsert exception {Exception}", err.Message);
        }

        return target;
    }

    private static Range[]? GetRepeatedRange(int count, int valueLenght, TaintedObject? taintedValue)
    {
        var valueToInsertRanges = taintedValue?.Ranges;
        if (valueToInsertRanges != null && count > 1)
        {
            var originalValueLength = valueLenght / count;
            for (int i = 1; i < count; i++)
            {
                valueToInsertRanges = Ranges.MergeRanges((i * originalValueLength), valueToInsertRanges, taintedValue!.Ranges);
            }
        }

        return valueToInsertRanges;
    }

    private static Range[]? GetSubRange(int index, int charCount, TaintedObject? taintedValue)
    {
        var valueToInsertRanges = taintedValue?.Ranges;
        if (valueToInsertRanges != null)
        {
            valueToInsertRanges = Ranges.ForSubstring(index, charCount, valueToInsertRanges);
        }

        return valueToInsertRanges;
    }
}
