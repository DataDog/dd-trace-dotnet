// <copyright file="SamplingScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Samples.ExceptionGenerator
{
    internal class SamplingScenario
    {
        public void Run()
        {
            // First, throw 4000 exceptions
            new ParallelExceptionsScenario().Run();

            // Then, throw an exception of a type that wasn't seen before
            try
            {
                throw new InvalidOperationException("OK");
            }
            catch
            {
            }
        }
    }
}
