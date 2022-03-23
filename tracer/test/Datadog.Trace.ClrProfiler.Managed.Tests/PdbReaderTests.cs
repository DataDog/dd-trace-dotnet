// <copyright file="PdbReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.PDBs;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class PdbReaderTests
    {
        [Fact]
        public void ReadPDBs()
        {
            string assemblyFullPath = Assembly.GetExecutingAssembly().Location;
            using var pdbReader = DatadogPdbReader.CreatePdbReader(assemblyFullPath);

            var symbolMethod = pdbReader.ReadMethodSymbolInfo(MethodBase.GetCurrentMethod().MetadataToken);

            symbolMethod.SequencePoints.First().Document.URL.Should().EndWith("PdbReaderTests.cs");
        }
    }
}
