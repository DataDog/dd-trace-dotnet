// <copyright file="StringModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Iast.Propagation;

internal static class StringModuleImpl
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringModuleImpl));

    internal static TaintedObject? GetTainted(TaintedObjects taintedObjects, object? value)
    {
        return value == null ? null : taintedObjects.Get(value);
    }

    public static object? PropagateTaint(object input, object result, int offset = 0)
    {
        try
        {
            if (result is null)
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
            Log.Error(err, "StringModuleImpl.PropagateTaint exception");
        }

        return result;
    }

    /// <summary> Taints a string.Insert operation </summary>
    /// <param name="target"> original string </param>
    /// <param name="index"> start index </param>
    /// <param name="value"> value to insert </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringInsert(string target, int index, string value, string result)
    {
        try
        {
            if (string.IsNullOrEmpty(result))
            {
                return result;
            }

            // if we have the same target and result, that means that we have called insert() with an empty insert string
            if (result == target)
            {
#if NETFRAMEWORK
                // In .net462 (not in netcore or netstandard), the method creates in this case a new string with the same value but a different reference, so we need to taint it
                PropagateTaint(target, result);
#endif
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedTarget = GetTainted(taintedObjects, target);
            var taintedValue = GetTainted(taintedObjects, value);
            if (taintedValue == null && taintedTarget == null)
            {
                return result;
            }

            var newRanges1 = taintedTarget != null ? Ranges.ForRemove(index, target.Length, taintedTarget.Ranges) : null;
            var newRanges2 = taintedValue?.Ranges;
            var newRanges3 = taintedTarget != null ? Ranges.ForRemove(0, index, taintedTarget.Ranges) : null;

            var rangesTotal = (newRanges1?.Length ?? 0) + (newRanges2?.Length ?? 0) + (newRanges3?.Length ?? 0);
            var rangesResult = new Range[rangesTotal];

            if (newRanges1 != null)
            {
                Ranges.CopyShift(newRanges1, rangesResult, 0, 0);
            }

            if (newRanges2 != null)
            {
                Ranges.CopyShift(newRanges2, rangesResult, (newRanges1?.Length ?? 0), index);
            }

            if (newRanges3 != null)
            {
                Ranges.CopyShift(newRanges3, rangesResult, (newRanges1?.Length ?? 0) + (newRanges2?.Length ?? 0), index + value.Length);
            }

            taintedObjects.Taint(result, rangesResult);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringInsert exception {Exception}", err.Message);
        }

        return result;
    }

    /// <summary> Taints a string.Remove operation </summary>
    /// <param name="self"> original string </param>
    /// <param name="result"> Result </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="endIndex"> end index </param>
    /// <returns> result </returns>
    public static string OnStringRemove(string self, string result, int beginIndex, int endIndex)
    {
        try
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(result))
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedSelf = GetTainted(taintedObjects, self);
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
            Log.Error(err, "StringModuleImpl.OnStringRemove exception {Exception}", err.Message);
        }

        return result;
    }

    /// <summary> Taints a string.substring operation </summary>
    /// <param name="self"> original string </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static char[]? OnStringSubSequence(string self, int beginIndex, char[]? result)
    {
        try
        {
            if (result is null || result.Length == 0)
            {
                return result;
            }

            OnStringSubSequence(self, beginIndex, result, result.Length);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringSubSequence(string,int,char[]) exception");
        }

        return result;
    }

    /// <summary> Taints a string.substring operation </summary>
    /// <param name="self"> original string </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringSubSequence(string self, int beginIndex, string result)
    {
        try
        {
            if (string.IsNullOrEmpty(result) || self == result)
            {
                return result;
            }

            OnStringSubSequence(self, beginIndex, result, result.Length);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringSubSequence(string,int,string) exception");
        }

        return result;
    }

    /// <summary> Taints a string.substring operation </summary>
    /// <param name="self"> original string </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="result"> the substring result </param>
    /// <param name="resultLength"> Result's length </param>
    private static void OnStringSubSequence(string self, int beginIndex, object result, int resultLength)
    {
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

    public static string OnStringJoin(string result, IEnumerable<object> values, int startIndex = 0, int count = -1)
    {
        try
        {
            if (string.IsNullOrEmpty(result))
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            TaintedObjects taintedObjects = iastContext.GetTaintedObjects();

            var newRanges = new List<Range>();
            int pos = 0;
            int i = 0;
            foreach (var element in values)
            {
                if (i >= startIndex && (count < 0 || i < startIndex + count))
                {
                    pos = GetPositionAndUpdateRangesInStringJoin(taintedObjects, newRanges, pos, null, 1, element?.ToString() ?? string.Empty, false);
                }

                i++;
            }

            if (newRanges.Count > 0)
            {
                taintedObjects.Taint(result, newRanges.ToArray());
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringJoinChar exception");
        }

        return result;
    }

    public static string OnStringJoin(string result, string delimiter, IEnumerable<object> values, int startIndex = 0, int count = -1)
    {
        try
        {
            if (string.IsNullOrEmpty(result))
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            TaintedObjects taintedObjects = iastContext.GetTaintedObjects();

            var newRanges = new List<Range>();
            int pos = 0;

            // Delimiter info
            var delimiterRanges = GetTainted(taintedObjects, delimiter)?.Ranges;
            var delimiterHasRanges = delimiterRanges?.Length > 0;
            var delimiterLength = delimiter?.Length ?? 0;
            var valuesCount = values.Count();

            int i = 0;
            foreach (var element in values)
            {
                if (i >= startIndex && (count < 0 || i < startIndex + count))
                {
                    pos = GetPositionAndUpdateRangesInStringJoin(taintedObjects, newRanges, pos, delimiterRanges, delimiterLength, element?.ToString() ?? string.Empty, delimiterHasRanges && i < valuesCount - 1);
                }

                i++;
            }

            if (newRanges.Count > 0)
            {
                taintedObjects.Taint(result, newRanges.ToArray());
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringJoin exception");
        }

        return result;
    }

    // Iterates over the element and delimiter ranges (if necessary) to update them and calculate the
    // new pos value
    private static int GetPositionAndUpdateRangesInStringJoin(TaintedObjects taintedObjects, List<Range> newRanges, int pos, Range[]? delimiterRanges, int delimiterLength, string element, bool addDelimiterRanges)
    {
        if (!string.IsNullOrEmpty(element))
        {
            var elementTainted = GetTainted(taintedObjects, element);
            if (elementTainted != null)
            {
                var elementRanges = elementTainted.Ranges;
                if (elementRanges.Length > 0)
                {
                    for (int i = 0; i < elementRanges.Length; i++)
                    {
                        newRanges.Add(elementRanges[i].Shift(pos));
                    }
                }
            }
        }

        pos += element.Length;
        if (addDelimiterRanges && delimiterRanges != null)
        {
            for (int i = 0; i < delimiterRanges.Length; i++)
            {
                newRanges.Add(delimiterRanges[i].Shift(pos));
            }
        }

        pos += delimiterLength;
        return pos;
    }

    /// <summary> OnStringTrim with single trimchar </summary>
    /// <param name="self"> Param 1 </param>
    /// <param name="result"> Result </param>
    /// <param name="trimChar"> the trim char, null for char.IsWhiteSpace(self[indexLeft]) </param>
    /// <param name="left"> Apply left trim </param>
    /// <param name="right"> Apply right trim </param>
    public static string? OnStringTrim(string self, string result, char? trimChar, bool left, bool right)
    {
        try
        {
            if (string.IsNullOrEmpty(result) || ReferenceEquals(self, result))
            {
                return result;
            }

            if (left && !right)
            {
                return OnStringSubSequence(self, self.Length - result.Length, result);
            }
            else if (!left && right)
            {
                return OnStringSubSequence(self, 0, result);
            }
            else
            {
                int indexLeft = 0;

                while (indexLeft < self.Length && trimChar is null ? char.IsWhiteSpace(self[indexLeft]) : self[indexLeft] == trimChar)
                {
                    indexLeft++;
                }

                return OnStringSubSequence(self, indexLeft, result);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnStringTrim(string,string,char,bool,bool) exception");
        }

        return result;
    }

    /// <summary> OnStringTrim with a char array </summary>
    /// <param name="self"> Param 1 </param>
    /// <param name="result"> Result </param>
    /// <param name="trimChars"> the trim chars </param>
    /// <param name="left"> Apply left trim </param>
    /// <param name="right"> Apply right trim </param>
    public static string OnStringTrimArray(string self, string result, char[] trimChars, bool left, bool right)
    {
        try
        {
            if (string.IsNullOrEmpty(result))
            {
                return result;
            }

            if (left && !right)
            {
                return OnStringSubSequence(self, self.Length - result.Length, result);
            }
            else if (!left && right)
            {
                return OnStringSubSequence(self, 0, result);
            }
            else
            {
                int indexLeft = 0;
                bool found;
                do
                {
                    found = false;

                    for (int i = 0; i < trimChars.Length; i++)
                    {
                        if (self[indexLeft] == trimChars[i])
                        {
                            found = true;
                            indexLeft++;
                            break;
                        }
                    }
                }
                while (found && indexLeft < self.Length);

                return OnStringSubSequence(self, indexLeft, result);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl. OnStringTrimArray(string,string,char[],bool,bool) exception");
        }

        return result;
    }

    /// <summary> Mostly used overload </summary>
    /// <param name="left"> Param 1 </param>
    /// <param name="right"> Param 2</param>
    /// <param name="result"> Result </param>
    /// <param name="filter"> Literal filter </param>
    /// <returns> result </returns>
    public static string OnStringConcat(string left, string right, string result, AspectFilter filter = AspectFilter.None)
    {
        try
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right) || string.IsNullOrEmpty(result))
        {
                return result;
        }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
        {
                return result;
        }

            TaintedObjects taintedObjects = iastContext.GetTaintedObjects();
            TaintedObject? taintedLeft = filter != AspectFilter.StringLiteral_1 ? GetTainted(taintedObjects, left) : null;
            TaintedObject? taintedRight = filter != AspectFilter.StringLiteral_0 ? GetTainted(taintedObjects, right) : null;
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
                Ranges.CopyShift(taintedRight!.Ranges, ranges, 0, left.Length);
        }
        else
        {
            ranges = Ranges.MergeRanges(left.Length, taintedLeft!.Ranges, taintedRight!.Ranges);
        }

            taintedObjects.Taint(result, ranges);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat(string,string) exception");
        }

        return result;
    }

    /// <summary> Overload for multi params (up to 5) </summary>
    /// <param name="parameters"> StringConcat params struct </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringConcat(in StringConcatParams parameters, string result)
    {
        try
        {
            if (string.IsNullOrEmpty(result) || !parameters.CanBeTainted())
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            TaintedObjects taintedObjects = iastContext.GetTaintedObjects();

            Range[]? ranges = null;
            int length = 0;
            for (int parameterIndex = 0; parameterIndex < parameters.ParamCount; parameterIndex++)
            {
                var currentParameter = parameters[parameterIndex];
                if (string.IsNullOrEmpty(currentParameter))
                {
                    continue;
                }

                var parameterTainted = GetTainted(taintedObjects, currentParameter);
                if (parameterTainted != null)
                {
                    if (ranges == null)
                    {
                        if (length == 0)
                        {
                            ranges = parameterTainted.Ranges;
                        }
                        else
                        {
                            ranges = new Range[parameterTainted!.Ranges!.Length];
                            Ranges.CopyShift(parameterTainted!.Ranges, ranges, 0, length);
                        }

                        length += currentParameter!.Length;
                        continue;
                    }

                    ranges = Ranges.MergeRanges(length, ranges, parameterTainted.Ranges);
                }

                length += currentParameter!.Length;
            }

            if (ranges != null)
            {
                taintedObjects.Taint(result, ranges);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat(StringConcatParams) exception");
        }

        return result;
    }

    /// <summary> Overload for multi params (up to 5) </summary>
    /// <param name="parameters"> StringConcat params struct </param>
    /// <param name="result"> Result </param>
    /// <returns> result </returns>
    public static string OnStringConcat(IEnumerable parameters, string result)
    {
        try
        {
            if (string.IsNullOrEmpty(result))
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            TaintedObjects taintedObjects = iastContext.GetTaintedObjects();

            Range[]? ranges = null;
            int length = 0;
            foreach (var parameterAsObject in parameters)
            {
                var currentParameter = parameterAsObject?.ToString();
                if (string.IsNullOrEmpty(currentParameter))
                {
                    continue;
                }

                var taintedParameter = GetTainted(taintedObjects, currentParameter);
                if (taintedParameter != null)
                {
                    if (ranges == null)
                    {
                        if (length == 0)
                        {
                            ranges = taintedParameter.Ranges;
                        }
                        else
                        {
                            ranges = new Range[taintedParameter!.Ranges!.Length];
                            Ranges.CopyShift(taintedParameter!.Ranges, ranges, 0, length);
                        }

                        length += currentParameter!.Length;
                        continue;
                    }

                    ranges = Ranges.MergeRanges(length, ranges, taintedParameter.Ranges);
                }

                length += currentParameter!.Length;
            }

            if (ranges != null)
            {
        taintedObjects.Taint(result, ranges);
    }
}
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.OnstringConcat(IEnumerable) exception");
        }

        return result;
    }

    public ref struct StringConcatParams
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
                    ThrowHelper.ThrowIndexOutOfRangeException("Invalid index in StringConcatParams");
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
                if (!string.IsNullOrEmpty(this[x]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
