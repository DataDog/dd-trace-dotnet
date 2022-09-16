// <copyright file="MockTracerResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers
{
    public class MockTracerResponse
    {
        public int StatusCode { get; set; } = 200;

        public string Response { get; set; }

        public bool SendResponse { get; set; } = true;
    }
}
