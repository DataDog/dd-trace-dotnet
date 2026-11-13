// <copyright file="EverGrowingCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class EverGrowingCollectionTests
    {
        [Theory]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(32)]
        [InlineData(33)]
        [InlineData(16, 64)]
        public void TryCreate_GrowsToFitIndex(params int[] indices)
        {
            var method = MethodBase.GetCurrentMethod();
            var collection = new MethodMetadataCollection();

            foreach (var index in indices)
            {
                collection.TryCreateNonAsyncMethodMetadataIfNotExists(index, method.MethodHandle, typeof(EverGrowingCollectionTests).TypeHandle).Should().BeTrue();
            }

            foreach (var index in indices)
            {
                collection.IndexExists(index).Should().BeTrue();
                collection.Get(index).Method.Should().Be(method);
            }
        }
    }
}
