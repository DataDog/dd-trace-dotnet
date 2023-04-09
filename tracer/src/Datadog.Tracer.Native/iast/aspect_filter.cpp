#include "aspect_filter.h"
#include "iast_util.h"
#include "module_info.h"

namespace iast
{
    AspectFilter::AspectFilter(ModuleAspects* module) { this->_module = module; }
    AspectFilter::~AspectFilter()
    {
        _module = nullptr;
    }

    AspectFilterTarget::AspectFilterTarget(const WSTRING& assembly, const WSTRING& typeName, const WSTRING& methods)
    {
        this->_assembly = SplitParams(assembly);
        this->_typeName = typeName;
        this->_methods = SplitParams(methods);
    }
    std::set<mdMemberRef> AspectFilterTarget::Resolve(ModuleInfo* module)
    {
        std::set<mdMemberRef> res;
        for (const auto& assembly : _assembly)
        {
            mdTypeRef typeRef;
            if (SUCCEEDED(module->GetAssemblyTypeRef(assembly, _typeName, &typeRef)))
            {
                for (const auto& method : _methods)
                {
                    std::vector<mdMemberRef> memberRefs;
                    if (FAILED(module->FindMemberRefsByName(typeRef, method, memberRefs)))
                    {
                        continue;
                    }

                    for (const auto& memberRef : memberRefs)
                    {
                        res.insert(memberRef);
                    }
                }
                break;
            }
        }
        return res;
    }
}