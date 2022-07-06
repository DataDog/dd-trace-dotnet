using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class LocalsCreator
    {
        private readonly ModuleDefMD _module;
        private readonly InstrumentedAssemblyGeneratorContext _context;
        private readonly InstrumentedMethod _method;

        internal LocalsCreator(InstrumentedMethod method, ModuleDefMD module, InstrumentedAssemblyGeneratorContext context)
        {
            _method = method;
            _module = module;
            _context = context;
        }

        internal IEnumerable<TypeSig> CreateTypesSig()
        {
            return _method.Locals.Select(local => local.GetTypeSig(_module, _context, null, 0));
        }
    }
}