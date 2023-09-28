// Decompiled with JetBrains decompiler
// Type: System.SequencePosition
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.ComponentModel;
using Datadog.System.Numerics.Hashing;

namespace Datadog.System
{
    public readonly struct SequencePosition : IEquatable<SequencePosition>
    {
        private readonly object _object;
        private readonly int _integer;

        public SequencePosition(object @object, int integer)
        {
            this._object = @object;
            this._integer = integer;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public object GetObject() => this._object;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int GetInteger() => this._integer;

        public bool Equals(SequencePosition other) => this._integer == other._integer && object.Equals(this._object, other._object);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) => obj is SequencePosition other && this.Equals(other);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            object obj = this._object;
            return HashHelpers.Combine(obj != null ? obj.GetHashCode() : 0, this._integer);
        }
    }
}
