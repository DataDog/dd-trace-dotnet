// <copyright file="ParseUtility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Propagators
{
    internal class ParseUtility
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ParseUtility>();

        public static ulong? ParseUInt64<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);
            var hasValue = false;

            foreach (string? headerValue in headerValues)
            {
                if (ulong.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                hasValue = true;
            }

            if (hasValue)
            {
                Log.Warning(
                    "Could not parse {HeaderName} headers: {HeaderValues}",
                    headerName,
                    string.Join(",", headerValues));
            }

            return null;
        }

        public static int? ParseInt32<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);
            bool hasValue = false;

            foreach (string? headerValue in headerValues)
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

            if (hasValue)
            {
                Log.Warning(
                    "Could not parse {HeaderName} headers: {HeaderValues}",
                    headerName,
                    string.Join(",", headerValues));
            }

            return null;
        }

        public static string? ParseString<TCarrier>(TCarrier headers, string headerName)
            where TCarrier : IHeadersCollection
        {
            var headerValues = headers.GetValues(headerName);

            foreach (string? headerValue in headerValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return headerValue;
                }
            }

            return null;
        }

        public static string? ParseString<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);

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
