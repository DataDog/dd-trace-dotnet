// <copyright file="IApiRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IApiRequest
    {
        void AddHeader(string name, string value);

        Task<IApiResponse> PostAsync(ArraySegment<byte> traces);
    }
}
