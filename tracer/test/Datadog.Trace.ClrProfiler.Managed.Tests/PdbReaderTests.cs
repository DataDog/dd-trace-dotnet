// <copyright file="PdbReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Pdb;
using Datadog.Trace.Pdb.SourceLink;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class PdbReaderTests
    {
        [Fact]
        public void ReadPDBs()
        {
            using var pdbReader = DatadogMetadataReader.CreatePdbReader(Assembly.GetExecutingAssembly());

            var sequencePoints = pdbReader?.GetMethodSequencePoints(MethodBase.GetCurrentMethod().MetadataToken);

            sequencePoints?.First().URL.Should().EndWith("PdbReaderTests.cs");
        }

        [Fact]
        public void ReadSourceLinkForGivenAssembly()
        {
            var datadogTraceAssembly = typeof(DatadogMetadataReader).Assembly;

            bool result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(datadogTraceAssembly, out string commitSha, out string repositoryUrl);
            result.Should().BeTrue();
            repositoryUrl.Should().BeOneOf(
                "https://github.com/DataDog/dd-trace-dotnet.git",
                "https://github.com/DataDog/dd-trace-dotnet");
            commitSha.Should().HaveLength(40);
            commitSha.Should().MatchRegex("[0-9a-f]+");
        }
    }
}
