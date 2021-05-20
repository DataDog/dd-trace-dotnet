using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    // TODO I think we'll need a 32-bit version of this as the object type pointers will change size
    [StructLayout(LayoutKind.Explicit)]
    internal struct PWArgs64
    {
        [FieldOffset(0)]
        public IntPtr ParameterName;

        [FieldOffset(8)]
        public ulong ParameterNameLength;

        // union
        // {
        // TODO this needs to be marshalled to string, since an object type can't share the same spaces as value type
        [FieldOffset(16)]
        public IntPtr StringValue;

        [FieldOffset(16)]
        public ulong UintValue;

        [FieldOffset(16)]
        public long IntValue;

        [FieldOffset(16)]
        public IntPtr PWArgs64Array;

        [FieldOffset(16)]
        public IntPtr RawHandle;
        // };

        [FieldOffset(24)]
        public ulong NbEntries;

        [FieldOffset(32)]
        public PW_INPUT_TYPE Type;
    }
}
