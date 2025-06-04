// <copyright file="TaintedObjects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Datadog.Trace.Iast
{
    internal class TaintedObjects
    {
        private static readonly bool _largeNumericCache = false;
        private readonly ITaintedMap _map;

        static TaintedObjects()
        {
            // From Net 8.0 onwards first 300 digit strings are cached instead of first 10
            _largeNumericCache = object.ReferenceEquals(299.ToString(), 299.ToString());
        }

        public TaintedObjects()
        {
            _map = new DefaultTaintedMap();
        }

#if NET6_0_OR_GREATER

        internal static unsafe string? GetString(ref ReadOnlySpan<char> span)
        {
            try
            {
#pragma warning disable CS8500
                // Gets the address of the first char
                fixed (void* spanPointer = &span)
                {
                    // Adjust for the string header (8 or 4 bytes) and length (4 bytes)
                    nint spanAddress = (*(nint*)spanPointer) - IntPtr.Size - 4;
                    string spanStr = *(string*)&spanAddress;
                    return spanStr;
                }
#pragma warning restore CS8500
            }
            catch (Exception ex)
            {
                IastModule.LogAspectException(ex, "Error while getting string from ReadOnlySpan");
            }

            return null;
        }
#endif

        public bool TaintInputString(string stringToTaint, Source source)
        {
            if (!IsFiltered(stringToTaint))
            {
                _map.Put(new TaintedObject(stringToTaint, IastUtils.GetRangesForString(stringToTaint, source)));
                return true;
            }

            bool IsFiltered(string arg)
            {
                // Try to bail out ASAP
                if (string.IsNullOrEmpty(arg)) { return true; }

                // 0 - 9 are cached only
                if (!_largeNumericCache)
                {
                    return arg.Length == 1 && char.IsDigit(arg[0]);
                }

                return arg.Length switch
                {
                    > 3 => false, // Only 0 - 299 are cached
                    _ when !char.IsDigit(arg[0]) => false, // Not a number
                    > 1 when !char.IsDigit(arg[1]) => false, // Not a number
                    > 2 when !char.IsDigit(arg[2]) => false, // Not a number
                    > 2 when arg[0] - '0' >= 3 => false, // Bigger than 299
                    _ => true
                };
            }

            return false;
        }

        public void Taint(object objectToTaint, Range[] ranges)
        {
            if (objectToTaint is not null)
            {
                var objectAsString = objectToTaint as string;
                if (objectAsString is null || objectAsString != string.Empty)
                {
                    _map.Put(new TaintedObject(objectToTaint, ranges));
                }
            }
        }

        public TaintedObject? Get(object objectToFind)
        {
            return _map.Get(objectToFind) as TaintedObject;
        }

#if NET6_0_OR_GREATER
        public TaintedObject? Get(ref ReadOnlySpan<char> objectToFind)
        {
            var stringToFind = GetString(ref objectToFind);
            if (stringToFind is null) { return null; }
            return _map.Get(stringToFind) as TaintedObject;
        }
#endif

        public int GetEstimatedSize()
        {
            return _map.GetEstimatedSize();
        }
    }
}
