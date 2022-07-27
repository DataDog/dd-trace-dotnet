// <copyright file="Trie.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datadog.Trace.Debugger.Configurations
{
    /// <summary>
    /// This Trie implementation is a direct translation of the Java debugger-agent implementation.
    /// There are 2 ways to use this Trie:
    /// 1. Insert full strings and match an exact string or a prefix on them
    /// 2. Insert prefix strings and match a full string on them (HasMatchingPrefix).
    /// 'prefixMode' indicates we have inserted prefixes inside the trie (usage 2.)
    /// </summary>
    internal class Trie
    {
        private readonly TrieNode _root;

        public Trie()
        {
            _root = new TrieNode((char)0);
        }

        public Trie(List<string> collection)
        {
            foreach (var str in collection)
            {
                Insert(str);
            }
        }

        public void Insert(string str)
        {
            var children = _root.Children;
            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];
                TrieNode node;
                if (children.ContainsKey(c))
                {
                    node = children[c];
                }
                else
                {
                    node = new TrieNode(c);
                    children[c] = node;
                }

                children = node.Children;
                node.IsLeaf = i == str.Length - 1;
                if (node.IsLeaf)
                {
                    node.Str = str;
                }
            }
        }

        /// <returns>True if the string exists in the trie.</returns>
        public bool Contains(string str)
        {
            var t = SearchNode(str, prefixMode: false);
            return t is { IsLeaf: true };
        }

        // <returns>True if there is any word in the trie that starts with the given prefix </returns>
        public bool ContainsPrefix(string prefix)
        {
            return SearchNode(prefix, prefixMode: false) != null;
        }

        // <returns>True if str matches one of the prefixes stored into the trie </returns>
        public bool HasMatchingPrefix(string str)
        {
            return SearchNode(str, prefixMode: true) != null;
        }

        // <returns>true is there is no string inserted into the Trie, otherwise false </returns>
        public bool IsEmpty()
        {
            return _root.Children.Count == 0;
        }

        /// <param name="prefix">prefix to search into the trie</param>
        /// <returns>the string if unique that matches the given prefix, otherwise null </returns>
        public string GetStringStartingWith(string prefix)
        {
            var node = SearchNode(prefix, prefixMode: false);
            if (node == null)
            {
                return null;
            }

            // while there is a unique path to the leaf, move forward
            while (!node.IsLeaf && node.Children.Count == 1)
            {
                node = node.Children.Values.First();
            }

            return node.Str;
        }

        /// <param name="str">String to search into the trie</param>
        /// <param name="prefixMode">prefixMode indicates String in the trie are prefixed and when reaching the leaf node we return it</param>
        /// <returns>last node that matches the whole given string or any prefix if prefixMode is true</returns>
        private TrieNode SearchNode(string str, bool prefixMode)
        {
            var children = _root.Children;
            TrieNode node = null;
            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];
                if (children.ContainsKey(c))
                {
                    node = children[c];
                    children = node.Children;
                    if (prefixMode && node.IsLeaf)
                    {
                        return node;
                    }
                }
                else
                {
                    return null;
                }
            }

            return node;
        }

        public static string ReverseStr(string str)
        {
            if (str == null)
            {
                return null;
            }

            return new string(new StringBuilder(str).ToString().Reverse().ToArray());
        }

        private class TrieNode
        {
            public TrieNode(char c)
            {
                Children = new Dictionary<char, TrieNode>();
                C = c;
            }

            public char C { get; set; }

            public Dictionary<char, TrieNode> Children { get; set; }

            public bool IsLeaf { get; set; }

            public string Str { get; set; }
        }
    }
}
