// <copyright file="FaultyApiRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.TestHelpers.TransportHelpers;

internal class FaultyApiRequest : TestApiRequest
{
    public FaultyApiRequest(Uri endpoint, int statusCode = 500)
        : base(endpoint, statusCode)
    {
    }
}
