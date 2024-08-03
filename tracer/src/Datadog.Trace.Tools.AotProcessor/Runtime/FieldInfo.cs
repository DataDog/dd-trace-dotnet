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
    internal class FieldInfo : MemberInfo
    {
        public FieldInfo(FieldDefinition definition, int id, ModuleInfo module)
            : base(definition, id, module)
        {
            Definition = definition;
        }

        public new FieldDefinition Definition { get; }

        public override int Attributes => (int)Definition.Attributes;
    }
}
