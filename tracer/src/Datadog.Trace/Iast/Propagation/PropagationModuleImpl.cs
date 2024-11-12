// <copyright file="PropagationModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

namespace Datadog.Trace.Iast.Propagation;

internal static class PropagationModuleImpl
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PropagationModuleImpl));

    public static void AddTaintedSource(string? input, Source source)
    {
        try
        {
            IastModule.OnExecutedSourceTelemetry((IastInstrumentedSources)source.Origin);

            if (input is null || input == string.Empty)
            {
                return;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext is null)
            {
                return;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedSelf = taintedObjects?.Get(input);

            if (taintedSelf is null)
            {
                taintedObjects?.TaintInputString(input, source);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "PropagationModuleImpl.AddTainted exception");
        }
    }

    /// <summary> Taints a string.Remove operation </summary>
    /// <param name="target"> original string </param>
    /// <param name="result"> Result </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="endIndex"> end index </param>
    /// <returns> result </returns>
    public static object? OnStringRemove(object? target, object? result, int beginIndex, int endIndex)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();
            if (result is null || target is null)
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedSelf = PropagationModuleImpl.GetTainted(taintedObjects, target);
            if (taintedSelf == null)
            {
                return result;
            }

            var newRanges = Ranges.ForRemove(beginIndex, endIndex, taintedSelf.Ranges);
            if (newRanges != null && newRanges.Length > 0)
            {
                taintedObjects.Taint(result, newRanges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "PropagationModuleImpl.OnStringRemove exception {Exception}", err.Message);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryPropagateWholeResult(string? result, object? input, out TaintedObjects? taintedObjects)
    {
        IastModule.OnExecutedPropagationTelemetry();
        if (result is null || input is null || result == string.Empty)
        {
            taintedObjects = null;
            return true;
        }

        if (object.ReferenceEquals(result, input))
        {
            taintedObjects = null;
            return true;
        }

        var iastContext = IastModule.GetIastContext();
        if (iastContext == null)
        {
            taintedObjects = null;
            return true;
        }

        taintedObjects = iastContext.GetTaintedObjects();

        if (result.Equals(input))
        {
            var tainted = taintedObjects.Get(input);
            if (tainted is not null)
            {
                taintedObjects.Taint(result, tainted.Ranges);
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PropagateWholeResultWhenInputTainted(string result, object? input, TaintedObjects taintedObjects)
    {
        if (input is not null)
        {
            var tainted = taintedObjects.Get(input);
            if (tainted?.Ranges?.Count() > 0 && tainted.Ranges[0].Source is not null && taintedObjects.Get(result) is null)
            {
                taintedObjects.Taint(result, new Range[] { new Range(0, result.Length, tainted.Ranges[0].Source, tainted.Ranges[0].SecureMarks) });
                return true;
            }
        }

        return false;
    }

    public static string? PropagateWholeResultWhenInputTainted(string? result, object? input1)
    {
        try
        {
            if (TryPropagateWholeResult(result, input1, out var taintedObjects) || taintedObjects is null)
            {
                return result;
            }

            PropagateWholeResultWhenInputTainted(result!, input1, taintedObjects);
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(PropagateWholeResultWhenInputTainted)} exception");
        }

        return result;
    }

    public static object? PropagateWholeResultWhenInputTainted(string? result, object? input1, object? input2)
    {
        try
        {
            if (TryPropagateWholeResult(result, input1, out var taintedObjects) || taintedObjects is null)
            {
                return result;
            }

            if (PropagateWholeResultWhenInputTainted(result!, input1, taintedObjects) ||
                PropagateWholeResultWhenInputTainted(result!, input2, taintedObjects))
            {
                return result;
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(PropagateWholeResultWhenInputTainted)} exception");
        }

        return result;
    }

    public static object? PropagateWholeResultWhenInputTainted(string? result, object? input1, object? input2, object? input3)
    {
        try
        {
            if (TryPropagateWholeResult(result, input1, out var taintedObjects) || taintedObjects is null)
            {
                return result;
            }

            if (PropagateWholeResultWhenInputTainted(result!, input1, taintedObjects) ||
                PropagateWholeResultWhenInputTainted(result!, input2, taintedObjects) ||
                PropagateWholeResultWhenInputTainted(result!, input3, taintedObjects))
            {
                return result;
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(PropagateWholeResultWhenInputTainted)} exception");
        }

        return result;
    }

    public static object? PropagateWholeResultWhenInputTainted(string? result, object? input1, object? input2, object? input3, object? input4)
    {
        try
        {
            if (TryPropagateWholeResult(result, input1, out var taintedObjects) || taintedObjects is null)
            {
                return result;
            }

            if (PropagateWholeResultWhenInputTainted(result!, input1, taintedObjects) ||
                PropagateWholeResultWhenInputTainted(result!, input2, taintedObjects) ||
                PropagateWholeResultWhenInputTainted(result!, input3, taintedObjects) ||
                PropagateWholeResultWhenInputTainted(result!, input4, taintedObjects))
            {
                return result;
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(PropagateWholeResultWhenInputTainted)} exception");
        }

        return result;
    }

    public static object? PropagateWholeResultWhenInputArrayTainted(string result, object? input1, object[]? otherInputs)
    {
        try
        {
            if (TryPropagateWholeResult(result, input1, out var taintedObjects) || taintedObjects is null)
            {
                return result;
            }

            if (PropagateWholeResultWhenInputTainted(result, input1, taintedObjects))
            {
                return result;
            }

            if (otherInputs?.Length > 0)
            {
                for (int i = 0; i < otherInputs.Length; i++)
                {
                    if (PropagateWholeResultWhenInputTainted(result, otherInputs[i], taintedObjects))
                    {
                        return result;
                    }
                }
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(PropagateWholeResultWhenInputArrayTainted)} exception");
        }

        return result;
    }

    public static string[]? PropagateWholeResultsWhenInputTainted(string[]? results, object? input)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();
            if (!(results?.Length > 0) || input is null)
            {
                return results;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return results;
            }

            var taintedObjects = iastContext.GetTaintedObjects();

            var tainted = taintedObjects.Get(input);
            if (tainted?.Ranges?.Length > 0)
            {
                var source = tainted.Ranges[0].Source;

                if (source is not null)
                {
                    for (int i = 0; i < results.Length; i++)
                    {
                        taintedObjects.Taint(results[i], new Range[] { new Range(0, results[i].Length, source) });
                    }
                }
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(PropagateWholeResultWhenInputTainted)} exception");
        }

        return results;
    }

    public static object? PropagateTaint(object? input, object? result, int offset = 0, bool addTelemetry = true)
    {
        try
        {
            if (addTelemetry)
            {
                IastModule.OnExecutedPropagationTelemetry();
            }

            if (result is null || input is null)
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedSelf = taintedObjects.Get(input);

            if (taintedSelf == null)
            {
                return result;
            }

            if (offset != 0)
            {
                var newRanges = new Range[taintedSelf.Ranges.Length];
                Ranges.CopyShift(taintedSelf.Ranges, newRanges, 0, offset);
                taintedObjects.Taint(result, newRanges);
            }
            else
            {
                taintedObjects.Taint(result, taintedSelf.Ranges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "PropagationModuleImpl.PropagateTaint exception");
        }

        return result;
    }

    public static TaintedObject? GetTainted(TaintedObjects taintedObjects, object? value)
    {
        return value == null ? null : taintedObjects.Get(value);
    }

    /// <summary> Taints a string.substring operation </summary>
    /// <param name="self"> original string </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="result"> the substring result </param>
    /// <param name="resultLength"> Result's length </param>
    /// <param name="addTelemetry"> true to add a telemetry instrumentation point </param>
    public static void OnStringSubSequence(object self, int beginIndex, object result, int resultLength, bool addTelemetry = true)
    {
        try
        {
            if (addTelemetry)
            {
                IastModule.OnExecutedPropagationTelemetry();
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var selfTainted = taintedObjects.Get(self);
            if (selfTainted == null)
            {
                return;
            }

            var rangesSelf = selfTainted.Ranges;
            if (rangesSelf.Length == 0)
            {
                return;
            }

            var newRanges = Ranges.ForSubstring(beginIndex, resultLength, rangesSelf);
            if (newRanges != null && newRanges.Length > 0)
            {
                taintedObjects.Taint(result, newRanges);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(OnStringSubSequence)} exception");
        }
    }

    // It could potentillay happen that a StringBuilder would have incorrect ranges. Reasons for that include: a not covered stringBuilder method
    // is used or a stringbuilder method is called through reflection. This topic has been discussed and it is an issue that could occur
    // in different languages.
    // This situation could affect the string builder class, but not the string class because the string methods return new instances
    // After discussion, we assume that we can have incorrect ranges in special situations, but we should make sure that the ranges do not
    // exceed the string length
    public static void FixRangesIfNeeded(string? result)
    {
        try
        {
            if (result is not null)
            {
                var tainted = IastModule.GetIastContext()?.GetTaintedObjects()?.Get(result);
                var ranges = tainted?.Ranges;

                if (ranges == null)
                {
                    return;
                }

                var incorrectRanges = false;
                List<Range>? newRanges = null;

                for (int i = 0; i < ranges.Length; i++)
                {
                    var range = ranges[i];
                    if (range.Start >= result.Length)
                    {
                        if (!incorrectRanges)
                        {
                            newRanges = FillValidRangesArray(ranges, i - 1);
                            incorrectRanges = true;
                        }
                    }
                    else if (range.Start + range.Length > result.Length)
                    {
                        if (!incorrectRanges)
                        {
                            newRanges = FillValidRangesArray(ranges, i - 1);
                            incorrectRanges = true;
                        }

                        newRanges?.Add(new Range(range.Start, result.Length - range.Start, range.Source));
                    }
                    else
                    {
                        if (incorrectRanges)
                        {
                            newRanges?.Add(range);
                        }
                    }
                }

                if (incorrectRanges)
                {
                    tainted!.Ranges = newRanges!.ToArray();
                }
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(PropagationModuleImpl)}.{nameof(FixRangesIfNeeded)} exception");
        }
    }

    private static List<Range> FillValidRangesArray(Range[] ranges, int index)
    {
        List<Range>? newRanges = new(ranges.Length);
        for (int previous = 0; previous <= index; previous++)
        {
            newRanges.Add(ranges[previous]);
        }

        return newRanges;
    }

    // Helper method to convert IEnumerable to IEnumerable<object>
    public static IEnumerable<object?> EnumerateAsObjects(IEnumerable enumerable)
    {
        foreach (var item in enumerable)
        {
            yield return item;
        }
    }
}
