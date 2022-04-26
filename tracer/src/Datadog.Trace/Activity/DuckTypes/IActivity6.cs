// <copyright file="IActivity6.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.DuckTypes
{
    internal interface IActivity6 : IActivity5
    {
        ActivityStatusCode Status { get; }

        string StatusDescription { get; }
    }
}
