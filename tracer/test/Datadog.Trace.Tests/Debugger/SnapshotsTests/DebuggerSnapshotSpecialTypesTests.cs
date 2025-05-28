// <copyright file="DebuggerSnapshotSpecialTypesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotSpecialTypesTests
    {
        static DebuggerSnapshotSpecialTypesTests()
        {
            // Configure Verify to use the Snapshots subdirectory
            VerifierSettings.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
        }

        [Fact]
        public async Task SpecialType_StringBuilder()
        {
            await ValidateSingleValue(new StringBuilder("hi from stringbuilder"));
        }

        [Fact]
        public async Task SpecialType_LazyUninitialized()
        {
            await ValidateSingleValue(new Lazy<int>(() => Math.Max(1, 2)));
        }

        [Fact]
        public async Task SpecialType_LazyInitialized()
        {
            var lazy = new Lazy<int>(() => Math.Max(1, 2));
            var temp = lazy.Value;
            await ValidateSingleValue(lazy);
        }

        /// <summary>
        /// Validate that we produce valid json for a specific value, and that the output conforms to the given set of limits on capture.
        /// </summary>
        internal async Task ValidateSingleValue(object local)
        {
            var snapshot = SnapshotBuilder.GenerateSnapshot(local);

            var verifierSettings = new VerifySettings();
            verifierSettings.ScrubLinesContaining(new[] { "id", "timestamp", "duration" });
            var localVariableAsJson = JObject.Parse(snapshot).SelectToken("debugger.snapshot.captures.return.locals");
            await Verifier.Verify(localVariableAsJson, verifierSettings);
        }
    }
}
