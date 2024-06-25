using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor.Runtime
{
    internal class MemberInfo : IDisposable
    {
        private GCHandle signature = default;

        public MemberInfo(MemberReference definition, int id, ModuleInfo module)
        {
            Definition = definition;
            Id = new MdMethodDef(id);
            Module = module;
            DeclaringType = new MdTypeDef(Definition.DeclaringType.MetadataToken.ToInt32());
        }

        public virtual void Dispose()
        {
            if (!signature.IsAllocated)
            {
                signature.Free();
            }
        }

        public MemberReference Definition { get; }
        public MdMethodDef Id { get; }
        public ModuleInfo Module { get; }

        public MdTypeDef DeclaringType { get; }

        public string Name { get => Definition.Name; }

        public virtual int Attributes { get => 0; }

        public virtual IntPtr GetSignature()
        {
            if (Definition.RawSignature is null || Definition.RawSignature.Length == 0)
            {
                return IntPtr.Zero;
            }

            if (!signature.IsAllocated)
            {
                signature = GCHandle.Alloc(Definition.RawSignature, GCHandleType.Pinned);
            }

            return signature.AddrOfPinnedObject();
        }

        public virtual uint SignatureLength => (uint)(Definition.RawSignature?.Length ?? 0);

        public virtual IntPtr GetRawBody()
        {
            return IntPtr.Zero;
        }

        public virtual uint RawBodyLength => 0;
    }
}
