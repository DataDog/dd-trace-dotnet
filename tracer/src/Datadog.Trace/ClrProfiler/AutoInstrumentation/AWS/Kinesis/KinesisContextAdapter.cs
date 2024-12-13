// <copyright file="KinesisContextAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    internal struct KinesisContextAdapter : IHeadersCollection, IBinaryHeadersCollection
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<KinesisContextAdapter>();
        private Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>();

        public KinesisContextAdapter()
        {
        }

        public Dictionary<string, object> GetDictionary()
        {
            var retval = new Dictionary<string, object>();
            foreach (string key in headers.Keys)
            {
                retval.Add(key, headers[key][0]);
            }

            return retval;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (headers.ContainsKey(name))
            {
                return headers[name];
            }

            return Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            Remove(name);
            Add(name, value);
        }

        public void Add(string name, string value)
        {
            if (headers.ContainsKey(name))
            {
                headers[name].Add(value);
            }
            else
            {
                var newValues = new List<string>();
                newValues.Add(value);
                headers.Add(name, newValues);
            }
        }

        public void Remove(string name)
        {
            headers.Remove(name);
        }

        public byte[] TryGetLastBytes(string name)
        {
            if (headers.ContainsKey(name))
            {
                return Convert.FromBase64String(headers[name][headers[name].Count - 1]);
            }

            return new byte[0];
        }

        public void Add(string name, byte[] value)
        {
            Add(name, Convert.ToBase64String(value));
        }
    }
}
