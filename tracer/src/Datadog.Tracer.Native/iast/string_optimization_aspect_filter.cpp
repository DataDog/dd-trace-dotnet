#include "string_optimization_aspect_filter.h"
#include "dataflow_aspects.h"

namespace iast
{
    const std::vector<AspectFilterTarget> targets =
    {
        {WStr("mscorlib,System.Runtime"), WStr("System.String"), WStr("StartsWith,Contains,op_Equality")},
        {WStr("mscorlib,System.Runtime.Extensions"), WStr("System.Convert"), WStr("ToInt32")}
    };

    StringOptimizationAspectFilter::StringOptimizationAspectFilter(ModuleAspects* module) :
        AspectFilter(module)
    {
        if (FAILED(ResolveTargetMemberRefs()))
        {
            trace::Logger::Error("StringOptimizationAspectFilter: Error getting target memberRefs");
        }
    }

    StringOptimizationAspectFilter::~StringOptimizationAspectFilter()
    {
        _targetMemberRefs.clear();
    }

    bool StringOptimizationAspectFilter::AllowInstruction(DataflowContext& context)
    {
        ILInstr* instruction = context.instruction;
        ILRewriter* processor = context.rewriter;

        if (_targetMemberRefs.empty()) { return true; }
        ILInstr* instr = instruction->m_pNext;
        // if the resulting string it is stored we cannot filter
        if (IsStLoc(instr->m_opcode)) { return true; }

        for (int i = 0; i < _stackLength; i++)
        {
            if (IsCall(instr->m_opcode)) 
            {
                return !IsTargetMemberRef(instr->m_Arg32);
            }
            
            if (instr != processor->GetILList()) 
            {
                instr = instr->m_pNext;
            }
            else 
            {
                break;
            }
        }
        return true;
    }

    bool StringOptimizationAspectFilter::IsTargetMemberRef(mdMemberRef memberRef)
    {
        return _targetMemberRefs.find(memberRef) != _targetMemberRefs.end();
    }

    HRESULT StringOptimizationAspectFilter::ResolveTargetMemberRefs()
    {
        for (auto target : targets)
        {
            auto targetRefs = target.Resolve(_module->_module);
            Add(_targetMemberRefs, targetRefs);
        }
        return (_targetMemberRefs.size() > 0) ? S_OK : S_FALSE;
    }

    bool StringOptimizationAspectFilter::IsStLoc(int opcode)
    {
        return opcode == CEE_STLOC ||
            opcode == CEE_STLOC_0 ||
            opcode == CEE_STLOC_1 ||
            opcode == CEE_STLOC_2 ||
            opcode == CEE_STLOC_3 ||
            opcode == CEE_STLOC_S ||
            opcode == CEE_STFLD ||
            opcode == CEE_STOBJ;
    }

    bool StringOptimizationAspectFilter::IsCall(int opcode)
    {
        return opcode == CEE_CALL ||
            opcode == CEE_CALLVIRT ||
            opcode == CEE_CALLI;
    }
}