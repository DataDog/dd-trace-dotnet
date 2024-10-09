#include "dataflow_il_analysis.h"
#include "dataflow_il_rewriter.h"
#include "iast_util.h"
#include "module_info.h"
#include "method_info.h"
#include "signature_info.h"
#include "signature_types.h"

namespace iast
{

    //-----------------------------------

#define OPCODEFLAGS_SizeMask        0x000F
#define OPCODEFLAGS_BranchTarget    0x0010
#define OPCODEFLAGS_Switch          0x0020
#define OPCODEFLAGS_Method          0x0040
#define OPCODEFLAGS_Field           0x0080
#define OPCODEFLAGS_String          0x0100
#define OPCODEFLAGS_Type            0x0200

    static const UINT16 _instructionFlags[] = {
    #define InlineNone           0
    #define ShortInlineVar       1
    #define InlineVar            2
    #define ShortInlineI         1
    #define InlineI              4
    #define InlineI8             8
    #define ShortInlineR         4
    #define InlineR              8
    #define ShortInlineBrTarget  1 | OPCODEFLAGS_BranchTarget
    #define InlineBrTarget       4 | OPCODEFLAGS_BranchTarget
    #define InlineMethod         4 | OPCODEFLAGS_Method
    #define InlineField          4 | OPCODEFLAGS_Field
    #define InlineType           4 | OPCODEFLAGS_Type
    #define InlineString         4 | OPCODEFLAGS_String
    #define InlineSig            4
    #define InlineRVA            4
    #define InlineTok            4
    #define InlineSwitch         0 | OPCODEFLAGS_Switch

    #define OPDEF(c,s,pop,push,args,type,l,s1,s2,flow) args,
    #include "opcode.def"
    #undef OPDEF

    #undef InlineNone
    #undef ShortInlineVar
    #undef InlineVar
    #undef ShortInlineI
    #undef InlineI
    #undef InlineI8
    #undef ShortInlineR
    #undef InlineR
    #undef ShortInlineBrTarget
    #undef InlineBrTarget
    #undef InlineMethod
    #undef InlineField
    #undef InlineType
    #undef InlineString
    #undef InlineSig
    #undef InlineRVA
    #undef InlineTok
    #undef InlineSwitch
        0,                              // CEE_COUNT
        4 | OPCODEFLAGS_BranchTarget,   // CEE_SWITCH_ARG
    };
    static int _instructionPushes[] = {

    #define OPDEF(c,s,pop,push,args,type,l,s1,s2,ctrl) \
    	 push ,

    #define Push0    0
    #define Push1    1
    #define PushI    1
    #define PushI4   1
    #define PushR4   1
    #define PushI8   1
    #define PushR8   1
    #define PushRef  1
    #define VarPush  1          // Test code doesn't call vararg fcns, so this should not be used

    #include "opcode.def"

    #undef Push0   
    #undef Push1   
    #undef PushI   
    #undef PushI4  
    #undef PushR4  
    #undef PushI8  
    #undef PushR8  
    #undef PushRef 
    #undef VarPush 
    #undef OPDEF
        0,                              // CEE_COUNT
        0,                              // CEE_SWITCH_ARG
    };
    static int _instructionPops[] = {

    #define OPDEF(c,s,pop,push,args,type,l,s1,s2,ctrl) \
    	 pop ,

    #define Pop0    0
    #define Pop1    1
    #define PopI    1
    #define Pop4    1
    #define PopR4   1
    #define PopI8   1
    #define PopR8   1
    #define PopRef  1
    #define VarPop  1          // Test code doesn't call vararg fcns, so this should not be used

    #include "opcode.def"

    #undef Pop0   
    #undef Pop1   
    #undef PopI   
    #undef PopI4  
    #undef PopR4  
    #undef PopI8  
    #undef PopR8  
    #undef PopRef 
    #undef VarPop 
    #undef OPDEF
        0,                              // CEE_COUNT
        0,                              // CEE_SWITCH_ARG
    };
    static std::string _instructionNames[] = {
    #define OPDEF(c,s,pop,push,args,type,l,s1,s2,ctrl) \
    	 s ,
    #include "opcode.def"
    #undef OPDEF
        "CEE_COUNT",                              // CEE_COUNT
        "CEE_SWITCH_ARG",                         // CEE_SWITCH_ARG
    };

    enum class FlowControl
    {
        NEXT,
        BREAK,
        CALL,
        RETURN,
        BRANCH,
        COND_BRANCH,
        THROW,
        META
    };
    static FlowControl _instructionControlFlow[] = {
    #define OPDEF(c,s,pop,push,args,type,l,s1,s2,ctrl) \
    	 FlowControl:: ctrl ,
    #include "opcode.def"
    #undef OPDEF
        FlowControl::META,                         // CEE_COUNT
        FlowControl::META,                         // CEE_SWITCH_ARG
    };

    //-------------------------------

    InstructionInfo::InstructionInfo(ILAnalysis* analysis, ILInstr* instruction)
    {
        _analysis = analysis;
        _instruction = instruction;
    }

    bool InstructionInfo::IsCall() { return _instructionControlFlow[_instruction->m_opcode] == FlowControl::CALL; }
    bool InstructionInfo::IsCalli() { return _instruction->m_opcode == CEE_CALLI; }
    bool InstructionInfo::IsBranch() { return _instructionControlFlow[_instruction->m_opcode] == FlowControl::BRANCH; }
    bool InstructionInfo::IsConditionalBranch() { return _instructionControlFlow[_instruction->m_opcode] == FlowControl::COND_BRANCH; }
    bool InstructionInfo::IsThrow() { return _instructionControlFlow[_instruction->m_opcode] == FlowControl::THROW; }
    bool InstructionInfo::IsFinal() { return _instructionControlFlow[_instruction->m_opcode] == FlowControl::RETURN || IsThrow(); }
    bool InstructionInfo::IsSwitch() { return _instruction->m_opcode == CEE_SWITCH; }
    bool InstructionInfo::IsRet() { return _instruction->m_opcode == CEE_RET; }
    bool InstructionInfo::IsDup() { return _instruction->m_opcode == CEE_DUP; }
    bool InstructionInfo::IsAddressLoad()
    {
        return _instruction->m_opcode == CEE_LDARGA || _instruction->m_opcode == CEE_LDARGA_S ||
               _instruction->m_opcode == CEE_LDLOCA || _instruction->m_opcode == CEE_LDLOCA_S;
    }

    void InstructionInfo::ConvertToNonAddressLoad()
    {
        if (_instruction->m_opcode == CEE_LDARGA_S)
        {
            _instruction->m_opcode = CEE_LDARG_S;
        }
        else if (_instruction->m_opcode == CEE_LDLOCA_S)
        {
            _instruction->m_opcode = CEE_LDLOC_S;
        }
        else if (_instruction->m_opcode == CEE_LDARGA)
        {
            _instruction->m_opcode = CEE_LDARG;
        }
        else if (_instruction->m_opcode == CEE_LDLOCA)
        {
            _instruction->m_opcode = CEE_LDLOC;
        }
    }

    bool InstructionInfo::IsField() { return (_instructionFlags[_instruction->m_opcode] & OPCODEFLAGS_Field) == OPCODEFLAGS_Field; }
    bool InstructionInfo::IsLocal()
    {
        return _instruction->m_opcode == CEE_LDLOC || _instruction->m_opcode == CEE_LDLOC_S || _instruction->m_opcode == CEE_LDLOC_0 || _instruction->m_opcode == CEE_LDLOC_1 || _instruction->m_opcode == CEE_LDLOC_2 || _instruction->m_opcode == CEE_LDLOC_3
            || _instruction->m_opcode == CEE_STLOC || _instruction->m_opcode == CEE_STLOC_S || _instruction->m_opcode == CEE_STLOC_0 || _instruction->m_opcode == CEE_STLOC_1 || _instruction->m_opcode == CEE_STLOC_2 || _instruction->m_opcode == CEE_STLOC_3
            || _instruction->m_opcode == CEE_LDLOCA;
    }
    bool InstructionInfo::IsArgument()
    {
        return _instruction->m_opcode == CEE_LDARG || _instruction->m_opcode == CEE_LDARG_S || _instruction->m_opcode == CEE_LDARG_0 || _instruction->m_opcode == CEE_LDARG_1 || _instruction->m_opcode == CEE_LDARG_2 || _instruction->m_opcode == CEE_LDARG_3
            || _instruction->m_opcode == CEE_STARG || _instruction->m_opcode == CEE_STARG_S
            || _instruction->m_opcode == CEE_LDARGA;
    }

    int InstructionInfo::GetPopDelta() { return _instructionPops[_instruction->m_opcode]; }
    int InstructionInfo::GetPushDelta() { return _instructionPushes[_instruction->m_opcode]; }

    void InstructionInfo::Resolve(ILInstr* op, int paramIndex)
    {
        _operator = op;
        _paramIndex = paramIndex;
    }
    bool InstructionInfo::Equals(ILInstr* obj)
    {
        return _instruction == obj;
    }

    InstructionInfo* InstructionInfo::Execute(Branch& branch, Branches& branches, ILAnalysis& analysis, bool resolvingBranches)
    {
        _isExecuted = true;
        if (IsCall())
        {
            auto methodSig = GetArgumentSignature();
            if (methodSig == nullptr || methodSig->_returnType == nullptr)
            {
                analysis._error = "NULL signature";
                return nullptr;
            }
            auto paramCount = methodSig->GetEffectiveParamCount();
            int firstParam = 0;
            if (_instruction->m_opcode == CEE_NEWOBJ) { firstParam++; }

            // pop function pointer
            if (_instruction->m_opcode == CEE_CALLI)
            {
                auto i = branch.Pop(analysis, this);
                if (i == nullptr)
                {
                    return nullptr;
                }
                i->Resolve(_instruction, paramCount); //Address is last param (non visible)
            }
            // pop arguments
            for (int x = paramCount - 1; x >= firstParam; x--)
            {
                auto i = branch.Pop(analysis, this);
                if (i == nullptr)
                {
                    return nullptr;
                }
                i->Resolve(_instruction, x);
            }

            if (resolvingBranches && branch.Stack.size() == 0)
            {
                return nullptr;
            }

            // push return value
            if (methodSig->_returnType->GetCorElementType() != ELEMENT_TYPE_VOID || _instruction->m_opcode == CEE_NEWOBJ)
            {
                branch.Push(this, resolvingBranches);
            }
        }
        else if (IsRet())
        {
            Resolve(_instruction, 0);
            auto method = analysis.GetMethod();
            if (method->GetSignature()->_returnType->GetCorElementType() != ELEMENT_TYPE_VOID)
            {
                if (branch.Stack.size() == 0 && _next == nullptr && _prev != nullptr &&
                    _prev->_instruction->m_opcode == CEE_THROW) // Trhow before last ret
                {
                    return nullptr;
                }
                auto i = branch.Pop(analysis, this);
                if (i == nullptr)
                {
                    return nullptr;
                }
                i->Resolve(_instruction, 0);
            }
            return nullptr;
        }
        else if (IsDup())
        {
            auto i = branch.Pop(analysis, this);
            if (i == nullptr)
            {
                return nullptr;
            }
            i->Resolve(_instruction, 0);
            branch.Push(i, true);
            branch.Push(this, resolvingBranches);
        }
        else
        {
            for (int x = GetPopDelta() - 1; x >= 0; x--)
            {
                auto i = branch.Pop(analysis, this);
                if (i == nullptr)
                {
                    return nullptr;
                }
                i->Resolve(_instruction, x);
            }
            if (resolvingBranches && branch.Stack.size() == 0)
            {
                return nullptr;
            }
            for (int x = 0; x < GetPushDelta(); x++)
            {
                branch.Push(this, resolvingBranches);
            }

            if (IsConditionalBranch())
            {
                if (_next != nullptr && !resolvingBranches)
                {
                    if (IsSwitch())
                    {
                        for (auto jumpTarget : analysis.GetSwitchTargets(this))
                        {
                            branches.AddBranch(new Branch(jumpTarget, &branch.Stack));
                        }
                    }
                    else
                    {
                        //Create new branch
                        branches.AddBranch(new Branch(analysis.GetInstruction(_instruction->m_pTarget), &branch.Stack));
                    }
                }
            }
            else if (IsBranch())
            {
                Resolve(_instruction);
                return analysis.GetInstruction(_instruction->m_pTarget);
            }
            else if (IsFinal())
            {
                Resolve(_instruction);
                if (IsThrow())
                {
                    branch.Stack.clear();
                }
                return nullptr;
            }
        }
        if (!_isPushed)
        {
            Resolve(_instruction);
        }

        return _next;
    }

    mdToken GetTypeToken(SignatureType* type)
    {
        mdToken res = 0;
        if (type != nullptr)
        {
            auto typeToken = dynamic_cast<SignatureTokenType*>(type);
            if (typeToken != nullptr)
            {
                typeToken->GetToken(&res);
            }
        }
        return res;
    }

    mdToken InstructionInfo::InferTypeToken()
    {
        auto signature = GetArgumentSignature();
        if (!signature) { return 0; }
        int index = _instruction->m_Arg8;
        if (IsLocal() && index < (int)signature->_params.size())
        {
            return GetTypeToken(signature->_params[index]);
        }
        else if (IsArgument())
        {
            if (index == 0 && signature->HasThis())
            {
                return this->_analysis->_body->GetMethodInfo()->GetTypeDef();
            }
            if (signature->HasThis()) { index--; }
            return GetTypeToken(signature->_params[index]);
        }
        else if (signature->_returnType != nullptr)
        {
            return GetTypeToken(signature->_returnType);
        }
        return 0;
    }

    SignatureInfo* InstructionInfo::GetArgumentSignature()
    {
        auto method = _analysis->_body->GetMethodInfo();
        auto module = method->GetModuleInfo();
        if (IsCalli())
        {
            return module->GetSignature(_instruction->m_Arg32);
        }
        else if (IsCall() || IsField())
        {
            auto member = module->GetMemberRefInfo(_instruction->m_Arg32);
            if (member != nullptr)
            {
                return member->GetSignature();
            }
        }
        else if (IsArgument())
        {
            return method->GetSignature();
        }
        else if (IsLocal())
        {
            return _analysis->_body->GetLocalsSignature();
        }
        return nullptr;
    }

    //----------------------------------------------

    ILAnalysis::ILAnalysis(ILRewriter* body)
    {
        try
        {
            this->_body = body;
            _exceptionStack.push_back(
                new InstructionInfo(this, new ILInstr{nullptr, nullptr, CEE_LDLOC_3})); // Push exception in Stack

            int x = 0;
            for (ILInstr* pInstr = body->GetILList()->m_pNext; pInstr != body->GetILList(); pInstr = pInstr->m_pNext)
            {
                auto instruction = new InstructionInfo(this, pInstr);
                _instructions.push_back(instruction);
                if (x > 0)
                {
                    auto prev = _instructions[x - 1];
                    instruction->_prev = prev;
                    prev->_next = instruction;
                }
                x++;
            }
            Branches branches;
            Branches branchesToResolve;
            //Add entry branch
            branches.AddBranch(new Branch(_instructions[0]));
            AddHandlerBranches(branches);

            Branch* branch = nullptr;
            while ((branch = branches.PopFirst()) != nullptr)
            {
                while (branch->Instruction != nullptr && !branch->Instruction->_isExecuted)
                {
                    branch->Instruction = branch->Instruction->Execute(*branch, branches, *this);
                }
                if (!IsStackValid())
                {
                    DEL(branch);
                    break;
                }
                if (branch->Stack.size() > 0)
                {
                    branchesToResolve.AddBranch(branch, true);
                }
                else
                {
                    DEL(branch);
                }
            }
            while ((branch = branchesToResolve.PopFirst()) != nullptr && IsStackValid())
            {
                while (branch->Instruction != nullptr && branch->Stack.size() > 0)
                {
                    branch->Instruction = branch->Instruction->Execute(*branch, branches, *this, true);
                }
                if (branch->Stack.size() > 0)
                {
                    _error = "Stack excess found";// {0}", branch.Stack[0]);
                    DEL(branch);
                    break;
                }
                DEL(branch);
            }
        }
        catch (std::exception err)
        {
            _error = err.what();
            trace::Logger::Error("ERROR verfying ", body->GetMethodInfo()->GetFullName(), " : ", _error);
        }
        catch (...)
        {
            _error = "Fatal error";
            trace::Logger::Error("ERROR verfying ", body->GetMethodInfo()->GetFullName(), " : ", _error);
        }
    }
    ILAnalysis::~ILAnalysis()
    {
        for (auto instr : _instructions)
        {
            delete(instr);
        }
        _instructions.clear();
    }

    void ILAnalysis::AddHandlerBranches(Branches& branches)
    {
        //Create branches for handlers
        for (auto handler : _body->GetExceptionHandlers())
        {
            if ((handler->m_Flags & COR_ILEXCEPTION_CLAUSE_FILTER) == COR_ILEXCEPTION_CLAUSE_FILTER) //HandlerType == ExceptionHandlerType.Filter)
            {
                branches.AddBranch(new Branch(GetInstruction(handler->m_pFilter), &_exceptionStack));
                branches.AddBranch(new Branch(GetInstruction(handler->m_pHandlerBegin), &_exceptionStack));
            }
            else if ((handler->m_Flags & COR_ILEXCEPTION_CLAUSE_FINALLY) == COR_ILEXCEPTION_CLAUSE_FINALLY
                || (handler->m_Flags & COR_ILEXCEPTION_CLAUSE_FAULT) == COR_ILEXCEPTION_CLAUSE_FAULT)
            {
                branches.AddBranch(new Branch(GetInstruction(handler->m_pHandlerBegin)));
            }
            else// (handler.HandlerType == ExceptionHandlerType.Catch)
            {
                branches.AddBranch(new Branch(GetInstruction(handler->m_pHandlerBegin), &_exceptionStack));
            }
        }
    }

    bool ILAnalysis::IsStackValid()
    {
        return _error.length() == 0;
    }
    std::string ILAnalysis::GetError()
    {
        return _error;
    }
    bool ILAnalysis::IsResolved()
    {
        return IsStackValid() && GetUnresolvedInstructionsCount() == 0;
    }

    MethodInfo* ILAnalysis::GetMethod()
    {
        return _body->GetMethodInfo();
    }
    ILRewriter* ILAnalysis::GetMethodBody()
    {
        return _body;
    }

    std::vector<InstructionInfo*> ILAnalysis::GetInstructions()
    {
        return _instructions;
    }
    int ILAnalysis::GetUnresolvedInstructionsCount()
    {
        return (int)std::count_if(_instructions.begin(), _instructions.end(), [](InstructionInfo* i) { return !i->_isResolved; });
    }
    std::vector<InstructionInfo*> ILAnalysis::LocateCallParamInstructions(ILInstr* callInstruction, int paramIndex)
    {
        std::vector<InstructionInfo*> res;
        auto instruction = GetInstruction(callInstruction);
        if (instruction == nullptr || !instruction->IsCall() || paramIndex < 0)
        {
            return res;
        }
        while (instruction != nullptr)
        {
            if (instruction->_operator == callInstruction && instruction->_paramIndex == paramIndex)
            {
                //res.Insert(0, instruction.Instruction); 
                res.insert(res.begin(), instruction); //Instructions come in reverse order -> reorder
            }
            instruction = instruction->_prev;
        }
        return res;
    }

    InstructionInfo* ILAnalysis::GetInstruction(ILInstr* instr)
    {
        auto it = std::find_if(_instructions.begin(), _instructions.end(), [instr](InstructionInfo* i) { return i->Equals(instr); });
        if (it != _instructions.end())
        {
            return *it;
        }
        return nullptr;
    }
    InstructionInfo* ILAnalysis::GetBranchTarget(BYTE offset)
    {
        auto target = _body->GetInstrFromOffset(offset);
        return GetInstruction(target);
    }

    std::vector<InstructionInfo*> ILAnalysis::GetSwitchTargets(InstructionInfo* switchInstr)
    {
        std::vector<InstructionInfo*> res;
        if (switchInstr->IsSwitch())
        {
            int count = switchInstr->_instruction->m_Arg32;
            auto option = switchInstr->_instruction;
            for (int x = 0; x < count; x++)
            {
                option = option->m_pNext;
                auto target = GetInstruction(option->m_pTarget);
                res.push_back(target);
            }
        }
        return res;
    }

    static WSTRING GetIndent(size_t count)
    {
        return WSTRING((count + 1) * 4, ' ');
    }
    WSTRING HandlerEnter(std::vector<EHClause*>& nesting, EHClause* indent)
    {
        nesting.push_back(indent);
        return GetIndent(nesting.size());
    }
    WSTRING HandlerExit(std::vector<EHClause*>& nesting, EHClause** indent = nullptr)
    {
        if (nesting.size() > 0)
        {
            if (indent)
            {
                *indent = nesting.back();
            }
            nesting.pop_back();
        }
        return GetIndent(nesting.size());
    }
    bool CommonTry(EHClause* h1, EHClause* h2)
    {
        if (h1 == nullptr || h2 == nullptr)
        {
            return false;
        }
        return h1->m_pTryBegin == h2->m_pTryBegin && h1->m_pTryEnd == h2->m_pTryEnd;
    }
    void AddNonExistent(std::vector<EHClause*>* vec, EHClause* value)
    {
        if (find(vec->begin(), vec->end(), value) == vec->end())
        {
            vec->push_back(value);
        }
    }
    bool IsFinally(EHClause* h)
    {
        return (h->m_Flags | COR_ILEXCEPTION_CLAUSE_FINALLY) == COR_ILEXCEPTION_CLAUSE_FINALLY;
    }
    WSTRING GetTypeName(ILRewriter* body, mdToken token)
    {
        auto typeRef = body->GetMethodInfo()->GetModuleInfo()->GetTypeInfo(token);
        WSTRING typeName = typeRef != nullptr ? typeRef->GetName() : WStr("Unknown");
        return typeName;
    }
    WSTRING GetMemberName(ILRewriter* body, mdToken token)
    {
        auto memberRef = body->GetMethodInfo()->GetModuleInfo()->GetMemberRefInfo(token);
        WSTRING memberName = memberRef != nullptr ? memberRef->GetFullyQualifiedName() : WStr("Unknown");
        return memberName;
    }
    std::string GetString(ILRewriter* body, mdString token)
    {
        auto stringValue = body->GetMethodInfo()->GetModuleInfo()->GetUserString(token);
        return shared::ToString(stringValue);
    }

    std::string ToString(ILRewriter* body, ILInstr* instruction)
    {
        MemberRefInfo* memberRef = nullptr;
        ILInstr* switchOp = nullptr;
        std::string memberName;

        std::string argument = "";
        auto flags = _instructionFlags[instruction->m_opcode];
        switch (flags)
        {
        case 0:
            break;
        case 1:
            argument = Hex(instruction->m_Arg8);
            break;
        case 2:
            argument = Hex(instruction->m_Arg16);
            break;
        case 4:
            argument = Hex(instruction->m_Arg32);
            break;
        case 8:
            argument = Hex((ULONG)instruction->m_Arg64);
            break;
        case 4 | OPCODEFLAGS_String:
            argument = "[" + Hex(instruction->m_Arg32) + "] \"" + GetString(body, instruction->m_Arg32) + "\"";
            break;
        case 4 | OPCODEFLAGS_Type:
            memberName = shared::ToString(GetTypeName(body, instruction->m_Arg32));
            argument = "[" + Hex(instruction->m_Arg32) + "] " + memberName;
            if (instruction->IsDirty())
            {
                memberName = shared::ToString(GetTypeName(body, instruction->m_originalArg32));
                argument = argument + " Original: [" + Hex(instruction->m_originalArg32) + "] " + memberName;
            }
            break;
        case 4 | OPCODEFLAGS_Method:
        case 4 | OPCODEFLAGS_Field:
            memberName = shared::ToString(GetMemberName(body, instruction->m_Arg32));
            argument = "[" + Hex(instruction->m_Arg32) + "] " + memberName;
            if (instruction->IsDirty())
            {
                memberName = shared::ToString(GetMemberName(body, instruction->m_originalArg32));
                argument = argument + " Original: [" + Hex(instruction->m_originalArg32) + "] " + memberName;
            }
            break;
        case 1 | OPCODEFLAGS_BranchTarget:
        case 4 | OPCODEFLAGS_BranchTarget:
            argument = ToString(body, instruction->m_pTarget);
            break;
        case 0 | OPCODEFLAGS_Switch:
            switchOp = instruction->m_pNext;
            while (switchOp->m_opcode == CEE_SWITCH_ARG)
            {
                argument = argument + "IL_" + Hex(switchOp->m_pTarget->m_offset);
                switchOp = switchOp->m_pNext;
            }
            break;
        default:
            break;
        }
        auto sep = instruction->IsDirty() ? "*: " : " : ";
        auto res = "IL" + Hex(instruction->m_offset) + sep + _instructionNames[instruction->m_opcode] + " " + argument;
        return res;
    }

    void ILAnalysis::Dump(const std::string& extraMessage)
    {
        try
        {
            auto method = _body->GetMethodInfo();
            auto module = method->GetModuleInfo();
            trace::Logger::Info("Dumping IL ", extraMessage, " : ", method->GetFullName(), " ",
                                 module->GetModuleFullName(), " ... ");

            std::unordered_map<ILInstr*, std::vector<EHClause*>*> handlers;
            for (auto handler : _body->GetExceptionHandlers())// .ExceptionHandlers.OrderByDescending(h = > h.HandlerEnd.Offset))
            {
                if (handler->m_pTryBegin != nullptr)
                {
                    AddNonExistent(Get<ILInstr*, std::vector<EHClause*>>(handlers, handler->m_pTryBegin,
                                                                         []() { return new std::vector<EHClause*>(); }),
                                   handler);
                }
                if (handler->m_pTryEnd != nullptr)
                {
                    AddNonExistent(Get<ILInstr*, std::vector<EHClause*>>(handlers, handler->m_pTryEnd,
                                                                         []() { return new std::vector<EHClause*>(); }),
                                   handler);
                }
                if (handler->m_pFilter != nullptr)
                {
                    AddNonExistent(Get<ILInstr*, std::vector<EHClause*>>(handlers, handler->m_pFilter,
                                                                         []() { return new std::vector<EHClause*>(); }),
                                   handler);
                }
                if (handler->m_pHandlerBegin != nullptr)
                {
                    AddNonExistent(Get<ILInstr*, std::vector<EHClause*>>(handlers, handler->m_pHandlerBegin,
                                                                         []() { return new std::vector<EHClause*>(); }),
                                   handler);
                }
            }
            for (auto pair : handlers)
            {
                std::sort(pair.second->begin(), pair.second->end(), [](EHClause* h1, EHClause* h2) {return h1->m_pHandlerEnd->m_offset <= h2->m_pHandlerEnd->m_offset; });
            }

            std::vector<EHClause*> nesting;
            WSTRING indent = GetIndent(0);
            for (ILInstr* instruction = _body->GetILList()->m_pNext; instruction != _body->GetILList(); instruction = instruction->m_pNext)
            {
                if (instruction->m_opcode == CEE_SWITCH_ARG) { continue; }
                EHClause* exitBlock = nullptr;
                auto hs = Get<ILInstr*, std::vector<EHClause*>>(handlers, instruction);
                if (hs != nullptr)
                {
                    for (auto h : *hs)
                    {
                        auto lastBlock = nesting.size() > 0 ? nesting.back() : nullptr;
                        auto firstCommonTryIt = find_if(hs->begin(), hs->end(), [h](EHClause* n) {return CommonTry(n, h); });
                        bool firstCommonTry = firstCommonTryIt != hs->end() && *firstCommonTryIt == h;

                        auto lastCommonTryIt = find_if(hs->rbegin(), hs->rend(), [h](EHClause* n) {return CommonTry(n, h); });
                        bool lastCommonTry = lastCommonTryIt != hs->rend() && *lastCommonTryIt == h;
                        if (h->m_pTryBegin == instruction && firstCommonTry)
                        {
                            trace::Logger::Info(indent, "try");
                            trace::Logger::Info(indent, "{");
                            indent = HandlerEnter(nesting, h);
                        }
                        else if (h->m_pTryEnd == instruction && firstCommonTry && CommonTry(h, lastBlock))
                        {
                            indent = HandlerExit(nesting, &exitBlock);
                            trace::Logger::Info(indent, "}");
                        }

                        if (h->m_pFilter == instruction)
                        {
                            trace::Logger::Info(indent, "filter");
                            trace::Logger::Info(indent, "{");
                            indent = HandlerEnter(nesting, h);
                        }
                        else if (h->m_pHandlerBegin == instruction)
                        {
                            trace::Logger::Info(indent, (IsFinally(h) ? WStr("finally") : WStr("catch")));
                            trace::Logger::Info(indent, "{");
                            indent = HandlerEnter(nesting, h);
                        }
                    }
                }
                trace::Logger::Info(indent, ToString(_body, instruction)); // Write current instruction
                if (instruction->m_opcode == CEE_ENDFILTER || instruction->m_opcode == CEE_ENDFINALLY || instruction->m_opcode == CEE_LEAVE || instruction->m_opcode == CEE_LEAVE_S)
                {
                    if (nesting.size() > 0)
                    {
                        indent = HandlerExit(nesting);
                        trace::Logger::Info(indent, "}");
                    }
                }
            }
            while (nesting.size() > 0)
            {
                EHClause* exitBlock = nullptr;
                indent = HandlerExit(nesting, &exitBlock);
                trace::Logger::Info(indent, "}");
            }

            DEL_MAP_VALUES(handlers);
            trace::Logger::Info("Dump end");
        }
        catch (std::exception err)
        {
            trace::Logger::Error("ERROR in Dump: ", err.what());
        }

    }

    //----------------------------------------

    Branch::Branch(InstructionInfo* instruction, std::vector<InstructionInfo*>* stack)
    {
        this->Instruction = instruction;
        if (stack)
        {
            Stack = *stack;
        }
    }

    InstructionInfo* Branch::Pop(ILAnalysis& analysis, InstructionInfo* instruction)
    {
        if (Stack.size() == 0)
        {
            analysis._error = "Stack underflow"; // on " + instruction);
            return nullptr;
        }
        auto res = Stack.back();
        Stack.pop_back();
        return res;
    }
    void Branch::Push(InstructionInfo* i, bool force)
    {
        if (!i->_isPushed || force)
        {
            Stack.push_back(i);
            i->_isPushed = true;
        }
    }

    //------------------------------

    Branches::Branches()
    {
    }
    Branches::~Branches()
    {
        for (auto branch : branches)
        {
            delete(branch);
        }
        branches.clear();
    }

    void Branches::AddBranch(Branch* branch, bool force)
    {
        if (force || addedBranches.find(branch->Instruction) == addedBranches.end())
        {
            if (branch->Id == -1)
            {
                branch->Id = (int)addedBranches.size();
            }
            addedBranches.insert(branch->Instruction);
            branches.push_back(branch);
        }
    }
    Branch* Branches::PopFirst()
    {
        if (branches.size() == 0)
        {
            return nullptr;
        }
        auto res = branches[0];
        branches.erase(branches.begin());
        return res;

    }
}
