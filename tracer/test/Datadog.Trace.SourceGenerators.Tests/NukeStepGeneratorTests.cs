// <copyright file="NukeStepGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.SourceGenerators.InstrumentationDefinitions;
using Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests
{
    public class NukeStepGeneratorTests
    {
        [Fact]
        public void GivenCallTargetsCppFile_WhenCheckingAdoNetReaderDefinitions_CategoryIsExpected()
        {
            var defs = GetCallTargetIntegrations().Where(d => d.InstrumentationTypeName.StartsWith("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Reader")).ToList();
            defs.Count.Should().BeGreaterThan(0);

            foreach (var def in defs)
            {
                def.InstrumentationCategory.Should().Be(InstrumentationCategory.Iast);
            }
        }

        private static IEnumerable<CallTargetDefinitionSource> GetCallTargetIntegrations()
        {
            var defsFile = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "build", "supported_calltargets.g.json");
            var definitions = JsonConvert.DeserializeObject<CallTargetDefinitionSource[]>(File.ReadAllText(defsFile));
            return definitions;
        }
    }
}
