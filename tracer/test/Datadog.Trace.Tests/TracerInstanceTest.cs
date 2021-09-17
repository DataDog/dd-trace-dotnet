// <copyright file="TracerInstanceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit;

namespace Datadog.Trace.Tests
{
    [CollectionDefinition(nameof(TracerInstanceTest), DisableParallelization = true)]
    public class TracerInstanceTest
    {
        [Fact]
        public void NormalTracerInstanceSwap()
        {
            var tracerOne = new Tracer();
            var tracerTwo = new Tracer();

            Tracer.Instance = tracerOne;
            Assert.Equal(tracerOne, Tracer.Instance);

            Tracer.Instance = tracerTwo;
            Assert.Equal(tracerTwo, Tracer.Instance);

            Tracer.Instance = null;
            Assert.Null(Tracer.Instance);
        }

        [Fact]
        public void LockedTracerInstanceSwap()
        {
            var tracerOne = new Tracer();
            var tracerTwo = new LockedTracer();

            Tracer.Instance = tracerOne;
            Assert.Equal(tracerOne, Tracer.Instance);

            Tracer.Instance = null;
            Assert.Null(Tracer.Instance);

            // Set the locked tracer
            Tracer.Instance = tracerTwo;
            Assert.Equal(tracerTwo, Tracer.Instance);

            // We test the locked tracer cannot be replaced.
            Assert.Throws<Exception>(() => Tracer.Instance = tracerOne);

            Assert.Throws<Exception>(() => Tracer.Instance = null);
        }

        private class LockedTracer : Tracer, ILockedTracer
        {
        }
    }
}
