// <copyright file="UserDetails.Internal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if INCLUDE_ALL_PRODUCTS

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec;

namespace Datadog.Trace
{
    /// <summary>
    /// A data container class for the users details
    /// </summary>
    public partial struct UserDetails : IUserDetails
    {
    }
}

#endif
