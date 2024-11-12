#include "string_literal_aspect_filter.h"
#include "dataflow_il_rewriter.h"
#include "dataflow_il_analysis.h"
#include "signature_info.h"
#include "dataflow_aspects.h"

namespace iast
{
    const std::vector<AspectFilterTarget> targets =
    {
        {WStr("mscorlib,System.Runtime"), WStr("System.String"), WStr("ToUpper,ToLower,Trim,TrimEnd,TrimStart")}
    };
    const std::vector<int> allIndexes;

    StringLiteralsAspectFilter::StringLiteralsAspectFilter(ModuleAspects* module, bool any) : AspectFilter(module) 
    {
        if (FAILED(ResolveTargetMemberRefs()))
        {
            trace::Logger::Error("StringLiteralsAspectFilter: Error getting target memberRefs");
        }
    }

    HRESULT StringLiteralsAspectFilter::ResolveTargetMemberRefs()
    {
        for (auto target : targets)
        {
            Add(_targetMemberRefs, target.Resolve(_module->_module));
        }
        return _targetMemberRefs.size() ? S_OK : S_FALSE;
    }

    bool StringLiteralsAspectFilter::IsTargetMemberRef(mdMemberRef memberRef)
    {
        return _targetMemberRefs.find(memberRef) != _targetMemberRefs.end();
    }

    bool StringLiteralsAspectFilter::ComesFromStringLiteral(ILInstr* instruction, ILRewriter* processor)
    {
        //Look up in stack to find a final LDSTR
        auto analysis = processor->StackAnalysis();
        auto instr = analysis->GetInstruction(instruction);
        while (instr)
        {
            if(instr->_instruction->m_opcode == CEE_LDSTR) { return true; }
            if (!instr->IsCall() || !IsTargetMemberRef(instr->_instruction->m_Arg32)) { return false; }
            auto params = analysis->LocateCallParamInstructions(instr->_instruction, 0);
            if (params.size() != 1) 
            {
                return false; 
            }
            instr = params[0];
        }
        return false;
    }
    bool StringLiteralsAspectFilter::AllowInstruction(DataflowContext& context)
    {
        return AllowInstruction(context, allIndexes, false);
    }
    bool StringLiteralsAspectFilter::AllowInstruction(DataflowContext& context, const std::vector<int>& indexes,
                                                      bool any)
    {
        ILInstr* instruction = context.instruction;
        ILRewriter* processor = context.rewriter;

        auto params = indexes;
        if (params.size() == 0)
        {
            //Get all function param indexes
            auto instr = processor->StackAnalysis()->GetInstruction(instruction);
            auto signature = instr->GetArgumentSignature();
            if (signature && signature->GetType() == SignatureTypes::Method)
            {
                int paramCount = signature->GetEffectiveParamCount();
                for (int x = 0; x < paramCount; x++)
                {
                    params.push_back(x);
                }
            }
        }

        bool res = true;
        for (auto param : params)
        {
            for (auto iInfo : processor->StackAnalysis()->LocateCallParamInstructions(instruction, param)) //Locate param load instruction
            {
                bool isStringLiteral = ComesFromStringLiteral(iInfo->_instruction, processor);
                if (isStringLiteral && any) 
                {
                    return false; 
                }
                if (!isStringLiteral && !any) 
                {
                    return true; 
                }
                res = !isStringLiteral;
            }
        }
        return res;
    }


    StringLiteralsAspectFilter_Base::StringLiteralsAspectFilter_Base(ModuleAspects* module, std::vector<int> indexes, bool any) : AspectFilter(module)
    {
        this->_baseFilter = dynamic_cast<StringLiteralsAspectFilter*>(module->GetFilter(DataflowAspectFilterValue::StringLiterals));
        this->_indexes = indexes;
        this->_any = any;
    }
    StringLiteralsAspectFilter_Base::StringLiteralsAspectFilter_Base(ModuleAspects* module, int index) : StringLiteralsAspectFilter_Base(module, std::vector<int>{ index }) { }
    bool StringLiteralsAspectFilter_Base::AllowInstruction(DataflowContext& context)
    {
        return _baseFilter->AllowInstruction(context, _indexes, _any);
    }


    StringLiterals_AnyAspectFilter::StringLiterals_AnyAspectFilter(ModuleAspects* module) : StringLiteralsAspectFilter_Base(module, allIndexes, true) {}
    StringLiteral_0AspectFilter::StringLiteral_0AspectFilter(ModuleAspects* module) : StringLiteralsAspectFilter_Base(module, 0) {}
    StringLiteral_1AspectFilter::StringLiteral_1AspectFilter(ModuleAspects* module) : StringLiteralsAspectFilter_Base(module, 1) {}
}