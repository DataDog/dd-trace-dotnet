// <copyright file="ModuleLookupTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ModuleLookupTests
    {
        [Fact]
        public void Lookup_SystemData_Succeeds_WithTwentyConcurrentTries()
        {
            var tasks = new Task[20];
            var resetEvent = new ManualResetEventSlim(initialState: false);
            var bag = new ConcurrentBag<Module>();
            var systemDataGuid = typeof(System.Data.DataTable).Assembly.ManifestModule.ModuleVersionId;

            for (var i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    resetEvent.Wait();
                    bag.Add(ModuleLookup.Get(systemDataGuid));
                });
            }

            resetEvent.Set();

            Task.WaitAll(tasks);

            Assert.True(bag.All(m => m.ModuleVersionId == systemDataGuid) && bag.Count() == tasks.Length);
        }

        [Fact]
        public void Lookup_Self_Succeeds()
        {
            var expectedModule = typeof(ModuleLookupTests).Assembly.ManifestModule;
            var lookup = ModuleLookup.Get(expectedModule.ModuleVersionId);
            Assert.Equal(expectedModule, lookup);
        }

        [Fact]
        public void Lookup_DatadogTraceClrProfilerManaged_Succeeds()
        {
            var expectedModule = typeof(MethodBuilder<>).Assembly.ManifestModule;
            var lookup = ModuleLookup.Get(expectedModule.ModuleVersionId);
            Assert.Equal(expectedModule, lookup);
        }

        [Fact]
        public void Lookup_DatadogTrace_Succeeds()
        {
            var expectedModule = typeof(Span).Assembly.ManifestModule;
            var lookup = ModuleLookup.Get(expectedModule.ModuleVersionId);
            Assert.Equal(expectedModule, lookup);
        }

        [Fact]
        public void Lookup_SystemData_Succeeds()
        {
            var expectedModule = typeof(System.Data.DataTable).Assembly.ManifestModule;
            var lookup = ModuleLookup.Get(expectedModule.ModuleVersionId);
            Assert.Equal(expectedModule, lookup);
        }
    }
}
