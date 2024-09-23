// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#pragma once
#include "../../../../shared/src/native-src/pal.h"

namespace iast
{
    class ILAnalysis;
    class SignatureInfo;
    class ModuleInfo;
    class DataflowAspectReference;
    class MethodInfo;

    typedef enum
    {
#define OPDEF(c,s,pop,push,args,type,l,s1,s2,ctrl) c,
#include "opcode.def"
#undef OPDEF
        CEE_COUNT,
        CEE_SWITCH_ARG, // special internal instructions
    } OPCODE;

    struct ILInstr
    {
        ILInstr* m_pNext;
        ILInstr* m_pPrev;

        unsigned        m_opcode;
        unsigned        m_offset;
        bool            m_isNew;

        union
        {
            ILInstr* m_pTarget;
            INT8        m_Arg8;
            INT16       m_Arg16;
            INT32       m_Arg32;
            INT64       m_Arg64;
        };
        union
        {
            ILInstr* m_pOriginalTarget;
            INT8        m_originalArg8;
            INT16       m_originalArg16;
            INT32       m_originalArg32;
            INT64       m_originalArg64;
        };
        inline bool IsNew() { return m_isNew; }
        inline bool IsDirty() { return IsNew() || m_Arg64 != m_originalArg64; }
        inline int GetLine() { return (IsNew() && m_pNext) ? m_pNext->GetLine() : -((int)m_offset); }
    };

    struct EHClause
    {
        CorExceptionFlag            m_Flags;
        ILInstr* m_pTryBegin;
        ILInstr* m_pTryEnd;
        ILInstr* m_pHandlerBegin;    // First instruction inside the handler
        ILInstr* m_pHandlerEnd;      // Last instruction inside the handler
        union
        {
            DWORD                   m_ClassToken;   // use for type-based exception handlers
            ILInstr* m_pFilter;      // use for filter-based exception handlers (COR_ILEXCEPTION_CLAUSE_FILTER is set)
        };
    };

    class ILRewriter
    {
    public:
        ILRewriter(MethodInfo* methodInfo);//ModuleID moduleID, mdToken tkMethod);
        ~ILRewriter();
    private:
        MethodInfo*         m_methodInfo;

        mdSignature         m_tkLocalVarSig;
        unsigned            m_maxStack;
        unsigned            m_flags;
        bool                m_fGenerateTinyHeader;
        ILAnalysis*         _stackAnalysis = nullptr;
        SignatureInfo*      _localsSignature = nullptr;

        ILInstr             m_IL; // Double linked list of all il instructions

        unsigned            m_nEH;
        EHClause*           m_pEH;

        // Helper table for importing.  Sparse array that maps BYTE offset of beginning of an
        // instruction to that instruction's ILInstr*.  BYTE offsets that don't correspond
        // to the beginning of an instruction are mapped to NULL.
        ILInstr**           m_pOffsetToInstr;
        unsigned            m_nCodeSize;
        unsigned            m_nInstrs;
        BYTE*               m_pOutputBuffer;

        HRESULT ImportIL(LPCBYTE pIL);
        HRESULT ImportEH(const COR_ILMETHOD_SECT_EH* pILEH, unsigned nEH);
        void ComputeOffsets();
        void AdjustState(ILInstr* pNewInstr);

    public:
        MethodInfo* GetMethodInfo();
        bool IsDirty();
        //void SetDirty();
        HRESULT Import();
        HRESULT Import(LPCBYTE pMethodIL);
        ILInstr* NewILInstr(OPCODE opcode = CEE_COUNT, ULONG32 arg = 0, bool isNew = false);
        ILInstr* InsertBefore(ILInstr* pWhere, ILInstr* pWhat, bool updateReferences = true);
        ILInstr* InsertAfter(ILInstr* pWhere, ILInstr* pWhat, bool updateReferences = true);
        HRESULT Export();
        ILInstr* GetILList();
        ILInstr* GetInstrFromOffset(unsigned offset);
        std::vector<EHClause*> GetExceptionHandlers();
        ILAnalysis* StackAnalysis();

        mdSignature GetLocalsSignatureToken();
        void SetLocalsSignatureToken(mdSignature localVarSigToken);
        SignatureInfo* GetLocalsSignature();

        HRESULT AddProbeBefore(mdToken hookMethod, ILInstr* pInsertProbeBeforeThisInstr, OPCODE opCode = CEE_LDC_I4,
                               ULONG32* hookArg = nullptr);
        HRESULT AddProbeAfter(mdToken hookMethod, ILInstr* pInsertProbeAfterThisInstr, OPCODE opCode = CEE_LDC_I4,
                              ULONG32* hookArg = nullptr);

        HRESULT AddEnterProbe(mdToken onEnterHook, OPCODE opCode = CEE_LDC_I4, ULONG32* hookArg = nullptr);
        HRESULT AddExitProbe(mdToken onExitHook, OPCODE opCode = CEE_LDC_I4, ULONG32* hookArg = nullptr);
    };
}