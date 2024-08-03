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
    internal class MethodInfo : MemberInfo
    {
        private GCHandle rawBody = default;

        public MethodInfo(MethodDefinition definition, int id, ModuleInfo module)
            : base(definition, id, module)
        {
            Definition = definition;
        }

        public override void Dispose()
        {
            if (!rawBody.IsAllocated)
            {
                rawBody.Free();
            }

            base.Dispose();
        }

        public new MethodDefinition Definition { get; }

        public override int Attributes => (int)Definition.Attributes;

        public override IntPtr GetRawBody()
        {
            if (!rawBody.IsAllocated)
            {
                rawBody = GCHandle.Alloc(Definition.Body.RawBody, GCHandleType.Pinned);
            }

            return rawBody.AddrOfPinnedObject();
        }

        public override uint RawBodyLength => (uint)Definition.Body.RawBody.Length;
    }
}
