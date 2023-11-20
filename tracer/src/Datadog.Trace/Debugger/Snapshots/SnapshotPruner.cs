// <copyright file="SnapshotPruner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DatadogDebugger.Util
{
    internal enum State
    {
        Object,
        String,
        NotCaptured,
        Escape
    }

    internal class SnapshotPruner
    {
        private const string NotCapturedReason = "notCapturedReason";
        private const string Depth = "depth";
        private const string Pruned = "{\"pruned\":true}";

        private readonly Stack<Node> _stack = new Stack<Node>();

        private State? _state = State.Object;
        private int _currentLevel;
        private int _strMatchIdx;
        private State _stringMatchesState;
        private string _matchingString;
        private Node _root;

        private SnapshotPruner(string snapshot)
        {
            var index = 0;
            foreach (var c in snapshot)
            {
                _state = Parse(this, c, index++);

                if (_state == null)
                {
                    break;
                }
            }
        }

        public static string Prune(string snapshot, int maxTargetedSize, int minLevel)
        {
            var delta = Encoding.UTF8.GetByteCount(snapshot) - maxTargetedSize;
            if (delta <= 0)
            {
                return snapshot;
            }

            var snapshotPruner = new SnapshotPruner(snapshot);
            var leaves = snapshotPruner.GetLeaves(minLevel);

            var sortedLeaves = new SortedSet<Node>(new NodeComparer());
            foreach (var leaf in leaves)
            {
                sortedLeaves.Add(leaf);
            }

            var total = 0;
            var nodes = new Dictionary<int, Node>();
            while (sortedLeaves.Any())
            {
                Node leaf = sortedLeaves.Min;
                sortedLeaves.Remove(leaf);

                nodes[leaf.Start] = leaf;
                total += leaf.Size() - Pruned.Length;
                if (total > delta)
                {
                    break;
                }

                Node parent = leaf.Parent;
                if (parent == null)
                {
                    break;
                }

                parent.Pruned++;
                if (parent.Pruned >= parent.Children.Count && parent.Level >= minLevel)
                {
                    parent.NotCaptured = true;
                    parent.NotCapturedDepth = true;
                    sortedLeaves.Add(parent);
                    foreach (var child in parent.Children)
                    {
                        nodes.Remove(child.Start);
                        total -= child.Size() - Pruned.Length;
                    }
                }
            }

            var prunedNodes = nodes.Values.OrderBy(n => n.Start).ToList();
            var sb = new StringBuilder();
            sb.Append(snapshot.Substring(0, prunedNodes[0].Start));
            for (var i = 1; i < prunedNodes.Count; i++)
            {
                sb.Append(Pruned);
                var nextSegmentStart = prunedNodes[i - 1].End + 1;
                var nextSegmentLength = prunedNodes[i].Start - nextSegmentStart;
                sb.Append(snapshot.Substring(nextSegmentStart, nextSegmentLength));
            }

            sb.Append(Pruned);
            var lastSegmentStart = prunedNodes[prunedNodes.Count - 1].End + 1;
            if (lastSegmentStart < Encoding.UTF8.GetByteCount(snapshot))
            {
                sb.Append(snapshot.Substring(lastSegmentStart));
            }

            return sb.ToString();
        }

        private IEnumerable<Node> GetLeaves(int minLevel)
        {
            return _root?.GetLeaves(minLevel) ?? Enumerable.Empty<Node>();
        }

        internal State? Parse(SnapshotPruner pruner, char c, int index)
        {
            switch (_state)
            {
                case State.Object:
                    return ParseObject(pruner, c, index);
                case State.String:
                    return ParseString(pruner, c);
                case State.NotCaptured:
                    return ParseNotCaptured(pruner, c);
                case State.Escape:
                    return ParseEscape(pruner, c);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal State? ParseObject(SnapshotPruner pruner, char c, int index)
        {
            switch (c)
            {
                case '{':
                    var node = new Node(index, pruner._currentLevel++);
                    if (pruner._stack.Any())
                    {
                        node.Parent = pruner._stack.Peek();
                        node.Parent.Children.Add(node);
                    }

                    pruner._stack.Push(node);
                    return State.Object;

                case '}':
                    var completedNode = pruner._stack.Pop();
                    completedNode.End = index;
                    pruner._currentLevel--;
                    if (!pruner._stack.Any())
                    {
                        pruner._root = completedNode;
                        return null;
                    }

                    return State.Object;

                case '"':
                    pruner._strMatchIdx = 0;
                    pruner._matchingString = NotCapturedReason;
                    pruner._stringMatchesState = State.NotCaptured;
                    return State.String;

                default:
                    return State.Object;
            }
        }

        private State NotCapturedAction()
        {
            var lastNode = _stack.Peek();
            if (lastNode == null)
            {
                throw new InvalidOperationException("Stack is empty");
            }

            lastNode.NotCaptured = true;
            return _stringMatchesState;
        }

        internal State? ParseString(SnapshotPruner pruner, char c)
        {
            switch (c)
            {
                case '"':
                    if (pruner._strMatchIdx == Encoding.UTF8.GetByteCount(pruner._matchingString))
                    {
                        return NotCapturedAction();
                    }

                    return State.Object;

                case '\\':
                    pruner._strMatchIdx = -1;
                    return State.Escape;

                default:
                    if (pruner._strMatchIdx > -1)
                    {
                        if (c != pruner._matchingString[pruner._strMatchIdx++])
                        {
                            pruner._strMatchIdx = -1;
                        }
                    }

                    return State.String;
            }
        }

        internal State? ParseNotCaptured(SnapshotPruner pruner, char c)
        {
            switch (c)
            {
                case '"':
                    pruner._strMatchIdx = 0;
                    pruner._matchingString = Depth;
                    pruner._stringMatchesState = State.Object;
                    return State.String;

                case ' ':
                case ':':
                case '\n':
                case '\t':
                case '\r':
                    return State.NotCaptured;

                default:
                    return State.Object;
            }
        }

        internal State? ParseEscape(SnapshotPruner pruner, char c)
        {
            return State.String;
        }

        private class NodeComparer : IComparer<Node>
        {
            public int Compare(Node x, Node y)
            {
                int comparison = -x.NotCapturedDepth.CompareTo(y.NotCapturedDepth);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = -x.Level.CompareTo(y.Level);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = -x.NotCaptured.CompareTo(y.NotCaptured);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = -x.Size().CompareTo(y.Size());
                if (comparison != 0)
                {
                    return comparison;
                }

                return ReferenceEquals(x, y) ? 0 : -1;
            }
        }

        internal class Node : IComparable<Node>
        {
            public Node(int start, int level)
            {
                Start = start;
                Level = level;
            }

            public int Pruned { get; set; }

            public Node Parent { get; set; }

            public List<Node> Children { get; } = new List<Node>();

            public int Start { get; }

            public int End { get; set; }

            public int Level { get; }

            public bool NotCaptured { get; set; }

            public bool NotCapturedDepth { get; set; }

            public IEnumerable<Node> GetLeaves(int minLevel)
            {
                if (!Children.Any() && Level >= minLevel)
                {
                    return new[] { this };
                }

                return Children.SelectMany(child => child.GetLeaves(minLevel));
            }

            public int Size() => End - Start + 1;

            public int CompareTo(Node other)
            {
                int comparison = NotCapturedDepth.CompareTo(other.NotCapturedDepth);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = Level.CompareTo(other.Level);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = NotCaptured.CompareTo(other.NotCaptured);
                if (comparison != 0)
                {
                    return comparison;
                }

                return Size().CompareTo(other.Size());
            }
        }
    }
}
