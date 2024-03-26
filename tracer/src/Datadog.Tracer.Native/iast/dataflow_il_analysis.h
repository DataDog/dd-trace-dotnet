#pragma once
#include "../../../../shared/src/native-src/pal.h"

namespace iast
{
    class ILRewriter;
    struct ILInstr;
    class Branch;
    class Branches;
    class ILAnalysis;
    class MethodInfo;
    class MemberRefInfo;
    class SignatureInfo;
    class InstructionInfo;
    class SignatureType;
    class DataflowAspects;

    class ILAnalysis
    {
        friend class InstructionInfo;
        friend class Branch;
    protected:
        ILRewriter* _body;
        std::vector<InstructionInfo*> _instructions;
        std::string _error;
        std::vector<InstructionInfo*> _exceptionStack;

        void AddHandlerBranches(Branches& branches);

    public:
        ILAnalysis(ILRewriter* body);
        virtual ~ILAnalysis();

        bool IsStackValid();
        std::string GetError();
        bool IsResolved();
        MethodInfo* GetMethod();
        ILRewriter* GetMethodBody();
        std::vector<InstructionInfo*> GetInstructions();
        InstructionInfo* GetInstruction(ILInstr* instr);
        int GetUnresolvedInstructionsCount();

        std::vector<InstructionInfo*> LocateCallParamInstructions(ILInstr* callInstruction, int paramIndex);

        void Dump(const std::string& extraMessage = "");

    protected:
        InstructionInfo* GetBranchTarget(BYTE offset);
        std::vector<InstructionInfo*> GetSwitchTargets(InstructionInfo* switchInstr);
    };

    class InstructionInfo
    {
        friend class ILAnalysis;
        friend class Branch;
    protected:
        ILAnalysis* _analysis;

        bool _isExecuted = false;
        bool _isPushed = false;
        bool _isResolved = false;
        ILInstr* _operator = nullptr;
        int _paramIndex = -1;
        InstructionInfo* _next = nullptr;
        InstructionInfo* _prev = nullptr;

    public:
        ILInstr* _instruction = nullptr;

    protected:
        void Resolve(ILInstr* op, int paramIndex = -1);
        int GetPopDelta();
        int GetPushDelta();
        InstructionInfo* Execute(Branch& branch, Branches& branches, ILAnalysis& analysis, bool resolvingBranches = false);

    public:
        InstructionInfo(ILAnalysis* analysis, ILInstr* instruction);

        bool Equals(ILInstr* obj);

        bool IsCall();
        bool IsCalli();
        bool IsBranch();
        bool IsConditionalBranch();
        bool IsSwitch();
        bool IsFinal();
        bool IsThrow();
        bool IsRet();
        bool IsDup();
        bool IsAddressLoad();

        bool IsField();
        bool IsLocal();
        bool IsArgument();

        void ConvertToNonAddressLoad();

        mdToken InferTypeToken();
        SignatureInfo* GetArgumentSignature();
    };

    class Branch
    {
    public:
        Branch(InstructionInfo* instruction, std::vector<InstructionInfo*>* stack = nullptr);

        InstructionInfo* Instruction;
        std::vector<InstructionInfo*> Stack;
        int Id;
        InstructionInfo* Pop(ILAnalysis& analysis, InstructionInfo* instruction);
        void Push(InstructionInfo* i, bool force = false);
    };

    class Branches
    {
    public:
        Branches();
        virtual ~Branches();

        std::vector<Branch*> branches;
        std::set<InstructionInfo*> addedBranches;
        void AddBranch(Branch* branch, bool force = false);
        Branch* PopFirst();
    };
}