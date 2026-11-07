// <copyright file="ReverseChainBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using FluentAssertions;
using ReferenceChainModel;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.ReferenceChain
{
    public class ReverseChainBuilderTests
    {
        [Fact]
        public void BuildPreservesOccurrencePaths()
        {
            // A -> B -> X
            // C -> B -> Y
            // Selecting X must not synthesize the unrelated C -> B -> X path.
            var tree = new ReferenceTree(
                version: 1,
                typeTable: ["A", "B", "X", "C", "Y"],
                roots:
                [
                    Root(0, "S", Node(1, Node(2))),
                    Root(3, "K", Node(1, Node(4))),
                ]);

            var selected = new ReverseChainBuilder(tree).Build(2).Should().ContainSingle().Which;
            var parent = selected.Parents.Should().ContainSingle().Which;
            var root = parent.Parents.Should().ContainSingle().Which;

            selected.TypeIndex.Should().Be(2);
            parent.TypeIndex.Should().Be(1);
            root.TypeIndex.Should().Be(0);
            root.IsRoot.Should().BeTrue();
            root.Parents.Should().BeEmpty();
        }

        [Fact]
        public void BuildHandlesRepeatedTypeOnAPathWithoutCreatingALoop()
        {
            // The serialized structure is an acyclic tree even though its type sequence is A -> B -> A.
            var tree = new ReferenceTree(
                version: 1,
                typeTable: ["A", "B"],
                roots:
                [
                    Root(0, "S", Node(1, Node(0))),
                ]);

            var selected = new ReverseChainBuilder(tree).Build(0).Should().ContainSingle().Which;
            var parent = selected.Parents.Should().ContainSingle().Which;
            var root = parent.Parents.Should().ContainSingle().Which;

            selected.TypeIndex.Should().Be(0);
            selected.IsRoot.Should().BeTrue();
            parent.TypeIndex.Should().Be(1);
            root.TypeIndex.Should().Be(0);
            root.IsRoot.Should().BeTrue();
            root.Parents.Should().BeEmpty();
        }

        private static ReferenceRootNode Root(int typeIndex, string categoryCode, params ReferenceNode[] children)
        {
            return new ReferenceRootNode(typeIndex, 1, 0, categoryCode, fieldName: null, children);
        }

        private static ReferenceNode Node(int typeIndex, params ReferenceNode[] children)
        {
            return new ReferenceNode(typeIndex, 1, 0, children);
        }
    }
}
