// <copyright file="IisAppType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public enum IisAppType
    {
        /// <summary>
        /// ASP.NET app using the Clr4IntegratedAppPool app pool
        /// </summary>
        AspNetIntegrated,

        /// <summary>
        /// ASP.NET app using the Clr4ClassicAppPool app pool
        /// </summary>
        AspNetClassic,

        /// <summary>
        /// ASP.NET Core using in-process hosting model and the UnmanagedClassicAppPool app pool
        /// </summary>
        AspNetCoreInProcess,

        /// <summary>
        /// ASP.NET Core using out-of-process hosting model and the UnmanagedClassicAppPool app pool
        /// </summary>
        AspNetCoreOutOfProcess,
    }
}
