// <copyright file="ParseUtility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class ParseUtility
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ParseUtility>();

        private static bool _firstWarning = true;

        public static ulong? ParseUInt64<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter getter, string headerName)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            var headerValues = getter.Get(carrier, headerName);
            var hasValue = false;

            if (headerValues is string[] stringValues)
            {
                // Checking string[] allows to avoid the enumerator allocation.
                foreach (string? headerValue in stringValues)
                {
                    if (ulong.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                    {
                        return result;
                    }

                    hasValue = true;
                }
            }
            else if (TryParse(headerValues, ref hasValue, out var result))
            {
                return result;
            }

            if (hasValue)
            {
                if (_firstWarning)
                {
                    Log.Warning(
                        "Could not parse {HeaderName} headers: {HeaderValues}",
                        headerName,
                        string.Join(",", headerValues));
                    _firstWarning = false;
                }
                else
                {
                    Log.Debug(
                        "Could not parse {HeaderName} headers: {HeaderValues}",
                        headerName,
                        string.Join(",", headerValues));
                }
            }

            return null;

            // IEnumerable version (different method to avoid try/finally in the caller)
            static bool TryParse(IEnumerable<string?> headerValues, ref bool hasValue, out ulong result)
            {
                result = 0;
                foreach (string? headerValue in headerValues)
                {
                    if (ulong.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                    {
                        return true;
                    }

                    hasValue = true;
                }

                return false;
            }
        }

        public static int? ParseInt32<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter getter, string headerName)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            var headerValues = getter.Get(carrier, headerName);
            bool hasValue = false;

            if (headerValues is string[] stringValues)
            {
                // Checking string[] allows to avoid the enumerator allocation.
                foreach (string? headerValue in stringValues)
                {
                    if (int.TryParse(headerValue, out var result))
                    {
                        // note this int value may not be defined in the enum,
                        // but we should pass it along without validation
                        // for forward compatibility
                        return result;
                    }

                    hasValue = true;
                }
            }
            else if (TryParse(headerValues, ref hasValue, out var result))
            {
                return result;
            }

            if (hasValue)
            {
                if (_firstWarning)
                {
                    Log.Warning(
                        "Could not parse {HeaderName} headers: {HeaderValues}",
                        headerName,
                        string.Join(",", headerValues));
                    _firstWarning = false;
                }
                else
                {
                    Log.Debug(
                        "Could not parse {HeaderName} headers: {HeaderValues}",
                        headerName,
                        string.Join(",", headerValues));
                }
            }

            return null;

            // IEnumerable version (different method to avoid try/finally in the caller)
            static bool TryParse(IEnumerable<string?> headerValues, ref bool hasValue, out int result)
            {
                result = 0;
                foreach (string? headerValue in headerValues)
                {
                    if (int.TryParse(headerValue, out result))
                    {
                        // note this int value may not be defined in the enum,
                        // but we should pass it along without validation
                        // for forward compatibility
                        return true;
                    }

                    hasValue = true;
                }

                return false;
            }
        }

        public static string? ParseString<TCarrier>(TCarrier headers, string headerName)
            where TCarrier : IHeadersCollection
        {
            var headerValues = headers.GetValues(headerName);

            if (headerValues is string[] stringValues)
            {
                // Checking string[] allows to avoid the enumerator allocation.
                foreach (string? headerValue in stringValues)
                {
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        return headerValue;
                    }
                }

                return null;
            }

            return ParseStringIEnumerable(headerValues);

            // IEnumerable version (different method to avoid try/finally in the caller)
            static string? ParseStringIEnumerable(IEnumerable<string?> headerValues)
            {
                foreach (string? headerValue in headerValues)
                {
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        return headerValue;
                    }
                }

                return null;
            }
        }

        public static string? ParseString<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter getter, string headerName)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            var headerValues = getter.Get(carrier, headerName);

            if (headerValues is string[] stringValues)
            {
                // Checking string[] allows to avoid the enumerator allocation.
                foreach (string? headerValue in stringValues)
                {
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        return headerValue;
                    }
                }

                return null;
            }

            return ParseStringIEnumerable(headerValues);

            // IEnumerable version (different method to avoid try/finally in the caller)
            static string? ParseStringIEnumerable(IEnumerable<string?> headerValues)
            {
                foreach (string? headerValue in headerValues)
                {
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        return headerValue;
                    }
                }

                return null;
            }
        }
    }
}
