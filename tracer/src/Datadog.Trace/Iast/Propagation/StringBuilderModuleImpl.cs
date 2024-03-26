// <copyright file="StringBuilderModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
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
            IastModule.OnExecutedPropagationTelemetry();
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

    public static StringBuilder? OnStringBuilderSubSequence(string? originalString, int beginIndex, int length, StringBuilder? result)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();
            if (result == null || string.IsNullOrEmpty(originalString))
            {
                return result;
            }

            PropagationModuleImpl.OnStringSubSequence(originalString!, beginIndex, result, length, addTelemetry: false);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringBuilderModuleImpl.OnStringBuilderSubSequence(string,int,int,StringBuilder) exception");
        }

        return result;
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
            IastModule.OnExecutedPropagationTelemetry();
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

            if (valueToInsertCharCount > 0)
            {
                valueToInsertRanges = GetSubRange(valueToInsertIndex, valueToInsertCharCount, taintedValue);
            }
            else
            {
                valueToInsertRanges = GetRepeatedRange(valueToInsertRepetitions, valueLenght, taintedValue);
            }

            var newRangesLeft = taintedTarget != null ? Ranges.ForRemove(index, target.Length, taintedTarget.Ranges) : null;
            var newRangesLeftLength = newRangesLeft?.Length ?? 0;

            var newRangesRight = taintedTarget != null ? Ranges.ForRemove(0, index, taintedTarget.Ranges) : null;
            var newRangesRightLength = newRangesRight?.Length ?? 0;

            var valueToInsertRangesLength = valueToInsertRanges?.Length ?? 0;
            var rangesResult = new RangeList(newRangesLeftLength + newRangesRightLength + valueToInsertRangesLength);
            rangesResult.Add(newRangesLeft, 0);
            rangesResult.Add(valueToInsertRanges, index);
            rangesResult.Add(newRangesRight, index + valueLenght);

            if (taintedTarget is null)
            {
                taintedObjects.Taint(target, rangesResult.ToArray());
            }
            else
            {
                taintedTarget.Ranges = rangesResult.ToArray();
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(StringBuilderModuleImpl)}.{nameof(OnStringBuilderInsert)} exception");
        }

        return target;
    }

    public static void FullTaintIfAnyTainted(StringBuilder target, object? firstInput = null, object? secondInput = null, object? thirdInput = null, object? fourthInput = null)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();
            if (target == null || target.Length == 0)
            {
                return;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext is null)
            {
                return;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var tainted = PropagationModuleImpl.GetTainted(taintedObjects, target);
            bool targetIsTainted = tainted is not null;

            if (!targetIsTainted)
            {
                if (((tainted = GetTaintedWithRanges(taintedObjects, firstInput)) is null) &&
                    ((tainted = GetTaintedWithRanges(taintedObjects, secondInput)) is null) &&
                    ((tainted = GetTaintedWithRanges(taintedObjects, thirdInput)) is null) &&
                    ((tainted = GetTaintedWithRanges(taintedObjects, fourthInput)) is null))
                {
                    return;
                }
            }

            var rangesResult = new Range[] { new Range(0, target.Length, tainted!.Ranges[0].Source) };
            if (!targetIsTainted)
            {
                taintedObjects.Taint(target, rangesResult);
            }
            else
            {
                tainted.Ranges = rangesResult;
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(StringBuilderModuleImpl)}.{nameof(FullTaintIfAnyTainted)} exception");
        }
    }

    public static void FullTaintIfAnyTainted(char[]? result, StringBuilder? firstInput)
    {
        if (result is not null && firstInput is not null)
        {
            FullTaintIfAnyTaintedAux(result, result.Length, firstInput, null);
        }
    }

    public static void FullTaintIfAnyTaintedEnumerable(StringBuilder target, string? firstInput, IEnumerable? otherInputs)
    {
        if (firstInput is not null || otherInputs is not null)
        {
            FullTaintIfAnyTaintedAux(target, target.Length, firstInput, otherInputs);
        }
    }

    private static void FullTaintIfAnyTaintedAux(object target, int targetLength, object? firstInput, IEnumerable? otherInputs)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();
            if (targetLength == 0)
            {
                return;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext is null)
            {
                return;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var tainted = PropagationModuleImpl.GetTainted(taintedObjects, target);
            bool targetIsTainted = tainted is not null;

            if (!targetIsTainted)
            {
                tainted = GetTaintedWithRanges(taintedObjects, firstInput);
                if (tainted is null && otherInputs is not null)
                {
                    foreach (var input in otherInputs)
                    {
                        tainted = GetTaintedWithRanges(taintedObjects, input);
                        if (tainted is not null)
                        {
                            break;
                        }
                    }
                }
            }

            if (tainted is null)
            {
                return;
            }

            var rangesResult = new Range[] { new Range(0, targetLength, tainted!.Ranges[0].Source) };

            if (!targetIsTainted)
            {
                taintedObjects.Taint(target, rangesResult);
            }
            else
            {
                tainted.Ranges = rangesResult;
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(StringBuilderModuleImpl)}.{nameof(FullTaintIfAnyTaintedAux)} exception");
        }
    }

    private static TaintedObject? GetTaintedWithRanges(TaintedObjects taintedObjects, object? value)
    {
        var tainted = PropagationModuleImpl.GetTainted(taintedObjects, value);
        return tainted is not null && tainted?.Ranges.Length > 0 ? tainted : null;
    }

    private static Range[]? GetRepeatedRange(int count, int valueLenght, TaintedObject? taintedValue)
    {
        var valueRanges = taintedValue?.Ranges;
        if (valueRanges != null && count > 1)
        {
            var originalValueLength = valueLenght / count;
            for (int i = 1; i < count; i++)
            {
                valueRanges = Ranges.MergeRanges((i * originalValueLength), valueRanges, taintedValue!.Ranges);
            }
        }

        return valueRanges;
    }

    private static Range[]? GetSubRange(int index, int charCount, TaintedObject? taintedValue)
    {
        return (taintedValue?.Ranges is null ? null : Ranges.ForSubstring(index, charCount, taintedValue.Ranges));
    }
}
