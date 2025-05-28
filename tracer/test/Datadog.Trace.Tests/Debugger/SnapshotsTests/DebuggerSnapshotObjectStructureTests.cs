// <copyright file="DebuggerSnapshotObjectStructureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests
{
    [UsesVerify]
    public class DebuggerSnapshotObjectStructureTests : DebuggerSnapshotCreatorTests
    {
        [Fact]
        public async Task ObjectStructure_Null()
        {
            await ValidateSingleValue(null);
        }

        [Fact]
        public async Task ObjectStructure_EmptyArray()
        {
            await ValidateSingleValue(new int[] { });
        }

        [Fact]
        public async Task ObjectStructure_EmptyList()
        {
            await ValidateSingleValue(new List<int>());
        }

        [Fact]
        public async Task EdgeCase_NullValues_ShouldProduceValidJson()
        {
            var snapshot = new SnapshotBuilder()
                .AddEntryArgument(null, "arg0")
                .AddReturnLocal(null, "local0")
                .Build();

            var json = ValidateJsonStructure(snapshot);

            var verifierSettings = CreateStandardVerifierSettings();
            await Verifier.Verify(NormalizeStackElement(snapshot), verifierSettings);
        }
    }
}
