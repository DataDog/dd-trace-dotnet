// <copyright file="TracerInstanceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.Tests
{
    public class TracerInstanceTest : TracerInstanceTestsBase
    {
        [Test]
        public void NormalTracerInstanceSwap()
        {
            var tracerOne = new Tracer();
            var tracerTwo = new Tracer();

            SetTracer(tracerOne);
            Assert.AreEqual(tracerOne, Tracer.Instance);

            SetTracer(tracerTwo);
            Assert.AreEqual(tracerTwo, Tracer.Instance);

            SetTracer(null);
            Assert.Null(Tracer.Instance);
        }

        [Test]
        public void LockedTracerInstanceSwap()
        {
            var tracerOne = new Tracer();
            var tracerTwo = new LockedTracer();

            SetTracer(tracerOne);
            Assert.AreEqual(tracerOne, Tracer.Instance);

            SetTracer(null);
            Assert.Null(Tracer.Instance);

            // Set the locked tracer
            SetTracer(tracerTwo);
            Assert.AreEqual(tracerTwo, Tracer.Instance);

            // We test the locked tracer cannot be replaced.
            Assert.Throws<InvalidOperationException>(() => Tracer.Instance = tracerOne);

            Assert.Throws<InvalidOperationException>(() => Tracer.Instance = null);
        }

        private class LockedTracer : Tracer, ILockedTracer
        {
        }
    }
}
