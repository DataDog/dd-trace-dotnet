// <copyright file="NullPowerWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf
{
    internal class NullPowerWaf : IPowerWaf
    {
        public IAdditiveContext CreateAdditiveContext()
        {
            return new NullAdditiveContext();
        }

        public void Dispose()
        {
        }
    }
}
