using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Datadog.Trace.Debugger.TimeTravel;

internal static class TimeTravelStateManager
{
    private static AsyncLocal<Stack<SnapshotMetadata>> _shadowStack = new();

    public static void StartMethod(Guid snapshotId, string className, string methodName)
    {
        if (_shadowStack.Value == null)
        {
            _shadowStack.Value = new Stack<SnapshotMetadata>();
        }

        _shadowStack.Value.Push(new SnapshotMetadata(snapshotId, _shadowStack.Value.Count > 0 ? _shadowStack.Value.Peek() : null));
    }
    
    public static void EndMethod()
    {
        _shadowStack.Value.Pop();
    }

    public static SnapshotMetadata GetParentSnapshotMetadata()
    {
        var stack = _shadowStack.Value;
        if (stack.Count > 0)
        {
            return stack.Peek().Parent;
        }

        return null;
    }

}

internal class SnapshotMetadata
{
    public Guid SnapshotId { get; }
    public SnapshotMetadata Parent { get; }

    public SnapshotMetadata(Guid snapshotId, SnapshotMetadata parent)
    {
        SnapshotId = snapshotId;
        Parent = parent;
    }
}
