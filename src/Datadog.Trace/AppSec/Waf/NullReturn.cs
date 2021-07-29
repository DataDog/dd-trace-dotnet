// <copyright file="NullReturn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf
{
    internal class NullReturn : IReturn
    {
        public ReturnCode ReturnCode => ReturnCode.Good;

        public string Data => string.Empty;

        public void Dispose()
        {
        }
    }
}
