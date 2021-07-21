// <copyright file="NullAdditiveContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class NullAdditiveContext : IAdditiveContext
    {
        public void Dispose()
        {
        }

        public IReturn Run(IDictionary<string, object> args)
        {
            return new NullReturn();
        }
    }
}
