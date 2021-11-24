// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tests
{
    public class PublicApiTests : PublicApiTestsBase
    {
        public PublicApiTests()
            : base(typeof(Tracer).Assembly)
        {
        }
    }
}
