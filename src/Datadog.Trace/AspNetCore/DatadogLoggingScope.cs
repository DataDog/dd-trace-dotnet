// <copyright file="DatadogLoggingScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Datadog.Trace.AspNetCore
{
    // Implementation based on AspNetCore Hosting implementation: https://github.com/dotnet/aspnetcore/blob/1a2b3260c6161ae9b7f639de228a6eb0488a1075/src/Hosting/Hosting/src/Internal/HostingLoggerExtensions.cs#L114
    internal class DatadogLoggingScope : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly string _service;
        private readonly string _env;
        private readonly string _version;
        private readonly string _traceId;

        private string _cachedToString;

        public DatadogLoggingScope()
        {
            _service = CorrelationIdentifier.Service;
            _env = CorrelationIdentifier.Env;
            _version = CorrelationIdentifier.Version;
            _traceId = CorrelationIdentifier.TraceId.ToString();
        }

        public int Count
        {
            get
            {
                return 4;
            }
        }

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new KeyValuePair<string, object>("dd_service", _service);
                }
                else if (index == 1)
                {
                    return new KeyValuePair<string, object>("dd_env", _env);
                }
                else if (index == 2)
                {
                    return new KeyValuePair<string, object>("dd_version", _version);
                }
                else if (index == 3)
                {
                    return new KeyValuePair<string, object>("dd_trace_id", _traceId);
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public override string ToString()
        {
            if (_cachedToString is null)
            {
                _cachedToString = string.Format(
                    CultureInfo.InvariantCulture,
                    "dd_service:{0} dd_env:{1} dd_version:{2} dd_trace_id:{3}",
                    _service,
                    _env,
                    _version,
                    _traceId);
            }

            return _cachedToString;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (int i = 0; i < Count; ++i)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
