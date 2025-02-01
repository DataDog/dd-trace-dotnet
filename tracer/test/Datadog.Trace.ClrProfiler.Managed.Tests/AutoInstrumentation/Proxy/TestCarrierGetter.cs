// <copyright file="TestCarrierGetter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy
{
    internal struct TestCarrierGetter : ICarrierGetter<NameValueHeadersCollection>
    {
        public IEnumerable<string?> Get(NameValueHeadersCollection carrier, string key)
        {
            return carrier.GetValues(key);
        }
    }
}
