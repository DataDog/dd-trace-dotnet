// <copyright file="Obj.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.WafEncoding
{
    // NOTE: this is referred to as ddwaf_object in the C++ code, we call it Obj to avoid a naming clash
    internal class Obj
    {
        private GCHandle? _handle;
        private DdwafObjectStruct _innerObj;

        /// <summary>
        /// Initializes a new instance of the <see cref="Obj"/> class.
        /// Obj encapsulates a ddwaf_object struct
        /// </summary>
        /// <param name="innerObj">the ddwaf struct</param>
        /// <param name="parentObj">if it's the top parent obj, we need to call the waf to dispose it otherwise we dont</param>
        public Obj(ref DdwafObjectStruct innerObj, bool parentObj = false)
        {
            _innerObj = innerObj;
            if (parentObj)
            {
                // we pin only the parent and the waf will dispose it as well as its children
                _handle = GCHandle.Alloc(_innerObj, GCHandleType.Pinned);
            }
        }

        public ObjType ArgsType
        {
            get { return EncoderLegacy.DecodeArgsType(_innerObj.Type); }
        }

        public long IntValue
        {
            get { return _innerObj.IntValue; }
        }

        public ulong UintValue
        {
            get { return _innerObj.UintValue; }
        }

        public nint InnerPtr
        {
            get { return _innerObj.Array; }
        }

        public ref DdwafObjectStruct InnerStruct
        {
            get { return ref _innerObj; }
        }

        public void DisposeAllChildren(WafLibraryInvoker wafLibraryInvoker)
        {
            if (_handle is not null && _handle.Value.Target is not null)
            {
                var item = (DdwafObjectStruct)_handle.Value.Target;
                wafLibraryInvoker.ObjectFree(ref item);
                _handle.Value.Free();
            }
        }
    }
}
