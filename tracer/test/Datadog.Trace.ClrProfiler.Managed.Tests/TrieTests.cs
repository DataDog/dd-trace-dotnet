// <copyright file="TrieTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests;

public class TrieTests
{
    public class TrieTest
    {
        [Fact]
        public void PrefixMatching()
        {
            var trie = new Trie();
            trie.Insert("foo");
            trie.Insert("bar");
            Assert.True(trie.HasMatchingPrefix("foobar"));
            Assert.True(trie.HasMatchingPrefix("barfoo"));
            Assert.False(trie.HasMatchingPrefix("fobar"));
            Assert.False(trie.HasMatchingPrefix("bafoo"));
            Assert.False(trie.HasMatchingPrefix(string.Empty));
        }

        [Fact]
        public void StartsWith()
        {
            var trie = new Trie();
            trie.Insert("abcde");
            trie.Insert("abcfe");
            Assert.True(trie.ContainsPrefix("a"));
            Assert.True(trie.ContainsPrefix("abc"));
            Assert.True(trie.ContainsPrefix("abcde"));
            Assert.True(trie.ContainsPrefix("abcfe"));
            Assert.False(trie.ContainsPrefix("sgsg"));
        }

        [Fact]
        public void GetStringStartingWith()
        {
            var trie = new Trie();
            trie.Insert("cs.ArrayExtensions/ExtensionMethods/Datadog.Trace/src/tracer/dd-trace-dotnet");
            trie.Insert("cs.LiveDebugger/Debugger/Datadog.Trace/src/tracer/dd-trace-dotnet/dev");
            Assert.EndsWith(
                "cs.ArrayExtensions/ExtensionMethods/Datadog.Trace/src/tracer/dd-trace-dotnet",
                trie.GetStringStartingWith("cs.ArrayExtensions/ExtensionMethods/Datadog.Trace/src"));
        }

        [Fact]
        public void GetStringStartingWithAmbiguous()
        {
            var trie = new Trie();
            trie.Insert("abcde");
            trie.Insert("abcfe");
            Assert.Null(trie.GetStringStartingWith("abc"));
        }

        [Fact]
        public void Empty()
        {
            var trie = new Trie();
            Assert.True(trie.IsEmpty());
            trie.Insert(string.Empty);
            Assert.True(trie.IsEmpty());
            trie.Insert("abc");
            Assert.False(trie.IsEmpty());
        }
    }
}
