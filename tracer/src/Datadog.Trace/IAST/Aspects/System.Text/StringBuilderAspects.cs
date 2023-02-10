// <copyright file="StringBuilderAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

namespace Datadog.Trace.Iast.Aspects.System.Text;

/// <summary> StringBuilder class aspects </summary>
[AspectClass("mscorlib,netstandard,System.Private.CoreLib")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public partial class StringBuilderAspects
{
#pragma warning disable S2259 // Null pointers should not be dereferenced

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <returns> New StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string value)
    {
        return StringBuilderModuleImpl.OnStringBuilderInit(new StringBuilder(value), value);
    }

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <param name="capacity"> StringBuilder initial capacity </param>
    /// <returns> Nerw StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string value, int capacity)
    {
        return StringBuilderModuleImpl.OnStringBuilderInit(new StringBuilder(value, capacity), value);
    }

    /// <summary>  StringBuilder.ToString aspect </summary>
    /// <param name="instance"> StringBuilder instance </param>
    /// <returns> instance.ToString() </returns>
    [AspectMethodReplace("System.Object::ToString()", "mscorlib|System.Text.StringBuilder")]
    public static string ToString(object instance)
    {
        return StringBuilderModuleImpl.OnStringBuilderToString(instance, instance.ToString());
    }

/*
        [AspectMethodReplace("System.Text.StringBuilder::ToString(System.Int32,System.Int32)")]
        public static string ToString(StringBuilder target, int startIndex, int length)
        {
            string result = target.ToString(startIndex, length);
            try
            {
                var context = ContextHolder.Current;
                var t = context.TaintedObjects;
                if (t != null)
                {
                    List<TaintedRange> ranges = SubStringRanges(context, target, startIndex, startIndex + length);
                    if (ranges.Count > 0)
                    {
                        t.TaintIfInputIsTainted(context, result, null, target, false, ranges);
                    }
                }
            }
            catch (AST.Commons.Exceptions.HdivException) { throw; }
            catch (Exception ex)
            {
                logger.Error(ex.ToFormattedString());
            }
            return result;
        }

        [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String,System.Int32,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
        public static StringBuilder Init(string value, int startIndex, int length, int capacity)
        {
            return TaintCompleteCharSequence(ContextHolder.Current, new StringBuilder(value, startIndex, length, capacity), value, startIndex, startIndex + length);
        }


        #region Append OK

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.String)", AspectFilter.StringLiteral_1)]
        public static StringBuilder Append(StringBuilder target, string value)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value), value, offset);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.Text.StringBuilder)")]
        public static StringBuilder Append(StringBuilder target, StringBuilder value)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value), value, offset);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.String,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
        public static StringBuilder Append(StringBuilder target, string value, int startIndex, int count)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value, startIndex, count), value, offset, startIndex, startIndex + count);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.Text.StringBuilder,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
        public static StringBuilder Append(StringBuilder target, StringBuilder value, int startIndex, int count)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value.ToString(), startIndex, count), value, offset, startIndex, startIndex + count);
        }


        [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char,System.Int32)")]
        public static StringBuilder Append(StringBuilder target, char value, int repeatCount)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value, repeatCount), value, offset, 0, 1);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char[],System.Int32,System.Int32)")]
        public static StringBuilder Append(StringBuilder target, char[] value, int startIndex, int charCount)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value, startIndex, charCount), value, offset, startIndex, startIndex + charCount);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.Object)")]
        public static StringBuilder Append(StringBuilder target, object value)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value), value, offset);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char[])")]
        public static StringBuilder Append(StringBuilder target, char[] value)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.Append(value), value, offset, 0, value.Length);
        }

        #endregion

        #region AppendFormat OK

        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object)")]
        public static StringBuilder AppendFormat(StringBuilder target, string format, object arg0)
        {
            return InternalAppendFormat(target, format, null, arg0);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object,System.Object)")]
        public static StringBuilder AppendFormat(StringBuilder target, string format, object arg0, object arg1)
        {
            return InternalAppendFormat(target, format, null, arg0, arg1);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object,System.Object,System.Object)")]
        public static StringBuilder AppendFormat(StringBuilder target, string format, object arg0, object arg1, object arg2)
        {
            return InternalAppendFormat(target, format, null, arg0, arg1, arg2);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object[])")]
        public static StringBuilder AppendFormat(StringBuilder target, string format, object[] args)
        {
            return InternalAppendFormat(target, format, null, args);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object)")]
        public static StringBuilder AppendFormat(StringBuilder target, IFormatProvider provider, string format, object arg0)
        {
            return InternalAppendFormat(target, format, provider, arg0);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object,System.Object)")]
        public static StringBuilder AppendFormat(StringBuilder target, IFormatProvider provider, string format, object arg0, object arg1)
        {
            return InternalAppendFormat(target, format, provider, arg0, arg1);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object,System.Object,System.Object)")]
        public static StringBuilder AppendFormat(StringBuilder target, IFormatProvider provider, string format, object arg0, object arg1, object arg2)
        {
            return InternalAppendFormat(target, format, provider, arg0, arg1, arg2);
        }
        [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object[])")]
        public static StringBuilder AppendFormat(StringBuilder target, IFormatProvider provider, string format, object[] args)
        {
            return InternalAppendFormat(target, format, provider, args);
        }

        #endregion

        #region AppendLine OK

        [AspectMethodReplace("System.Text.StringBuilder::AppendLine(System.String)", AspectFilter.StringLiteral_1)]
        public static StringBuilder AppendLine(StringBuilder target, string value)
        {
            int offset = target != null ? target.Length : 0;
            return InternalAppend(target.AppendLine(value), value, offset);
        }

        #endregion

        #region CopyTo - KO

        [AspectMethodReplace("System.Text.StringBuilder::CopyTo(System.Int32,System.Char[],System.Int32,System.Int32)")]
        public static void CopyTo(StringBuilder target, int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            target.CopyTo(sourceIndex, destination, destinationIndex, count);
            try
            {
                var context = ContextHolder.Current;
                var t = context.TaintedObjects;
                if (t != null)
                {
                    TaintIfInputIsTainted(context, destination, target, 0, count, 0);
                }
            }
            catch (AST.Commons.Exceptions.HdivException) { throw; }
            catch (Exception ex)
            {
                logger.Error(ex.ToFormattedString());
            }
        }

        #endregion

        #region Insert OK

        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.String)")]
        public static StringBuilder Insert(StringBuilder target, int index, string value)
        {
            if (value == null) { return target; }
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value, true, InsertRanges(context, target, value, index, value.Length));
        }

        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.String,System.Int32)")]
        public static StringBuilder Insert(StringBuilder target, int index, string value, int count)
        {
            if (value == null) { return target; }
            var context = ContextHolder.Current;
            var realValue = GetRealValueCount(context, value, count);
            return TaintCharSequence(context, target.Insert(index, value, count), realValue, true, InsertRanges(context, target, realValue, index, realValue.Length));
        }

        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Char)")]
        public static StringBuilder Insert(StringBuilder target, int index, char value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Char[])")]
        public static StringBuilder Insert(StringBuilder target, int index, char[] value)
        {
            if (value == null) { return target; }
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value, true, InsertRanges(context, target, value, index, value.Length));
        }

        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Char[],System.Int32,System.Int32)")]
        public static StringBuilder Insert(StringBuilder target, int index, char[] value, int startIndex, int charCount)
        {
            if (value == null) { return target; }
            var result = target.Insert(index, value, startIndex, charCount);
            try
            {
                var context = ContextHolder.Current;
                var t = context.TaintedObjects;
                if (t != null)
                {
                    char[] contrainedValue = new char[charCount];
                    Array.Copy(value, startIndex, contrainedValue, 0, charCount);
                    var originalValue = t.Get(value);
                    if (originalValue != null)
                    {
                        TaintIfInputIsTainted(context, contrainedValue, value, 0, contrainedValue.Length, 0);
                    }
                    return TaintCharSequence(context, result, contrainedValue, true, InsertRanges(context, target, contrainedValue, index, contrainedValue.Length));
                }
            }
            catch (AST.Commons.Exceptions.HdivException) { throw; }
            catch (Exception ex)
            {
                logger.Error(ex.ToFormattedString());
            }
            return result;
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Int32)")]
        public static StringBuilder Insert(StringBuilder target, int index, int value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Int64)")]
        public static StringBuilder Insert(StringBuilder target, int index, long value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Single)")]
        public static StringBuilder Insert(StringBuilder target, int index, float value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Double)")]
        public static StringBuilder Insert(StringBuilder target, int index, double value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Decimal)")]
        public static StringBuilder Insert(StringBuilder target, int index, decimal value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.UInt16)")]
        public static StringBuilder Insert(StringBuilder target, int index, ushort value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.UInt32)")]
        public static StringBuilder Insert(StringBuilder target, int index, uint value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.UInt64)")]
        public static StringBuilder Insert(StringBuilder target, int index, ulong value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Boolean)")]
        public static StringBuilder Insert(StringBuilder target, int index, bool value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(), true, InsertRanges(context, target, value, index, value.ToString().Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.SByte)")]
        public static StringBuilder Insert(StringBuilder target, int index, sbyte value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Byte)")]
        public static StringBuilder Insert(StringBuilder target, int index, byte value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Int16)")]
        public static StringBuilder Insert(StringBuilder target, int index, short value)
        {
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(CultureInfo.CurrentCulture), true, InsertRanges(context, target, value, index, value.ToString(CultureInfo.CurrentCulture).Length));
        }
        [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Object)")]
        public static StringBuilder Insert(StringBuilder target, int index, object value)
        {
            if (value == null) { return target; }
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Insert(index, value), value.ToString(), true, InsertRanges(context, target, value, index, value.ToString().Length));
        }

        #endregion

        #region Remove

        [AspectMethodReplace("System.Text.StringBuilder::Remove(System.Int32,System.Int32)")]
        public static StringBuilder Remove(StringBuilder target, int startIndex, int length)
        {
            // we need to review the funcionality because we only need to update ranges if stringbuilder is already tainted
            var context = ContextHolder.Current;
            return TaintCharSequence(context, target.Remove(startIndex, length), target.ToString(), false, DeleteRanges(context, target, startIndex, startIndex + length));
        }

        #endregion

        #region Replace
        private static void UpdateIndexes(List<int> indexesToReplace, int indexToReplace, int offset)
        {
            for (int i = 0; i < indexesToReplace.Count; i++)
            {
                if (indexesToReplace[i] > indexToReplace)
                {
                    indexesToReplace[i] += offset;
                }
            }
        }
        private static List<int> AllIndexesOf(string str, string value, int startIndex)
        {
            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length)
            {
                index = str.IndexOf(value, index);
                if (index == -1)
                {
                    return indexes;
                }
                indexes.Add(startIndex + index);
            }
        }

        [AspectMethodReplace("System.Text.StringBuilder::Replace(System.String,System.String)")]
        public static StringBuilder Replace(StringBuilder target, string oldValue, string newValue)
        {
            return Replace(target, oldValue, newValue, 0, target.Length);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Replace(System.String,System.String,System.Int32,System.Int32)")]
        public static StringBuilder Replace(StringBuilder target, string oldValue, string newValue, int startIndex, int count)
        {
            try
            {
                var context = ContextHolder.Current;
                var t = context.TaintedObjects;
                if (t != null)
                {
                    var index = target.ToString().IndexOf(oldValue, startIndex, count);
                    if (newValue == null) { newValue = String.Empty; }
                    if (index < -1)
                    {
                        return TaintReplacement(context, target.Replace(oldValue, newValue, startIndex, count), newValue);
                    }

                    var indexesToReplace = AllIndexesOf(target.ToString().Substring(startIndex, count), oldValue, startIndex);
                    for (int i = 0; i < indexesToReplace.Count; i++)
                    {
                        int startIndex_ = indexesToReplace[i];
                        int endIndex_ = startIndex_ + oldValue.Length;

                        List<TaintedRange> ranges = ReplaceRanges(context, target, newValue, startIndex_, endIndex_, newValue.Length);
                        target = TaintCharSequence(context, target.Replace(oldValue, newValue, startIndex_, endIndex_ - startIndex_), newValue, true, ranges);

                        UpdateIndexes(indexesToReplace, indexesToReplace[i], newValue.Length - oldValue.Length);
                    }
                    return target;
                }
            }
            catch (AST.Commons.Exceptions.HdivException) { throw; }
            catch (Exception ex)
            {
                logger.Error(ex.ToFormattedString());
            }
            return target.Replace(oldValue, newValue, startIndex, count);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Replace(System.Char,System.Char)")]
        public static StringBuilder Replace(StringBuilder target, char oldChar, char newChar)
        {
            return Replace(target, oldChar.ToString(), newChar.ToString(), 0, target.Length);
        }

        [AspectMethodReplace("System.Text.StringBuilder::Replace(System.Char,System.Char,System.Int32,System.Int32)")]
        public static StringBuilder Replace(StringBuilder target, char oldChar, char newChar, int startIndex, int count)
        {
            return Replace(target, oldChar.ToString(), newChar.ToString(), startIndex, count);
        }

        #endregion

        #region Length

        [AspectMethodReplace("System.Text.StringBuilder::set_Length(System.Int32)")]
        public static void SetLength(StringBuilder target, int length)
        {
            var initialLength = target.Length;
            target.Length = length;
            try
            {
                var context = ContextHolder.Current;
                var t = context.TaintedObjects;
                var o = t?.Get(target);
                if (o != null)
                {
                    DeleteRanges(context, target, length, initialLength);
                    if (o.Ranges.IsEmpty())
                    {
                        t.Remove(target);
                    }
                }
            }
            catch (AST.Commons.Exceptions.HdivException) { throw; }
            catch (Exception ex)
            {
                logger.Error(ex.ToFormattedString());
            }
        }
        #endregion


        #region Aux Methods

        private static StringBuilder TaintReplacement(IHttpContext context, StringBuilder result, params object[] args)
        {
            var t = context.TaintedObjects;
            if (t != null)
            {
                t.TaintIfAnyoneIsTainted(context, result, t.Get(result), args);
            }
            return result;
        }
        private static string GetRealValueCount(IHttpContext context, string value, int count)
        {
            if (count <= 0)
            {
                return "";
            }

            string realValue = "";
            for (int i = 0; i < count; i++)
            {
                realValue = StringConcatTainted(context, new string[] { realValue, value });
            }

            return realValue;
        }

        #endregion
    */
#pragma warning restore S2259 // Null pointers should not be dereferenced
}
