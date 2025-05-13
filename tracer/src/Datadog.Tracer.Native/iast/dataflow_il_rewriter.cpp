// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "dataflow_il_rewriter.h"
#include "iast_util.h"
#include "cor.h"
#include "corprof.h"
#include <cassert>
#include "module_info.h"
#include "method_info.h"
#include "aspect.h"
#include "dataflow_aspects.h"
#include "dataflow_il_analysis.h"
#include "signature_info.h"

namespace iast
{

#define OPCODEFLAGS_SizeMask        0x0F
#define OPCODEFLAGS_BranchTarget    0x10
#define OPCODEFLAGS_Switch          0x20

    static const BYTE s_OpCodeFlags[] =
    {
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
    #define InlineMethod         4
    #define InlineField          4
    #define InlineType           4
    #define InlineString         4
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

    static int k_rgnStackPushes[] = {

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

#undef assert
#define assert(x)

    ILRewriter::ILRewriter(MethodInfo* methodInfo) : m_fGenerateTinyHeader(false), m_pEH(nullptr), m_pOffsetToInstr(nullptr), m_pOutputBuffer(nullptr)
    {
        m_methodInfo = methodInfo;

        m_IL.m_pNext = &m_IL;
        m_IL.m_pPrev = &m_IL;

        m_nInstrs = 0;
    }

    ILRewriter::~ILRewriter()
    {
        ILInstr* p = m_IL.m_pNext;
        while (p != &m_IL)
        {
            ILInstr* t = p->m_pNext;
            delete p;
            p = t;
        }
        DEL_ARR(m_pEH);
        DEL_ARR(m_pOffsetToInstr);
        DEL_ARR(m_pOutputBuffer);
        DEL(_stackAnalysis);
        DEL(_localsSignature);
    }

    MethodInfo* ILRewriter::GetMethodInfo()
    {
        return m_methodInfo;
    }
    bool ILRewriter::IsDirty()
    {
        for (ILInstr* pInstr = m_IL.m_pNext; pInstr != &m_IL; pInstr = pInstr->m_pNext)
        {
            if (pInstr->IsDirty()) 
            {
                return true; 
            }
        }
        return false;
    }
    HRESULT ILRewriter::Import()
    {
        HRESULT hr = S_OK;
        LPCBYTE pMethodBytes;

        IfFailRet(m_methodInfo->GetMethodIL(&pMethodBytes, nullptr));
        return Import(pMethodBytes);
    }

    HRESULT ILRewriter::Import(LPCBYTE pMethodIL)
    {
        HRESULT hr = S_OK;
        COR_ILMETHOD_DECODER decoder((COR_ILMETHOD*)pMethodIL);

        // Import the header flags
        m_tkLocalVarSig = decoder.GetLocalVarSigTok();
        m_maxStack = decoder.GetMaxStack();
        m_flags = (decoder.GetFlags() & CorILMethod_InitLocals);

        m_nCodeSize = decoder.GetCodeSize();
        auto ehCount = decoder.EHCount();
        if (ehCount < 0 || ehCount > m_nCodeSize) 
        {
            trace::Logger::Info("ILRewriter::Import -> Detected incoherent EH size on ", m_methodInfo->GetFullName());
            return E_FAIL;
        }

        IfFailRet(ImportIL(decoder.Code));
        IfFailRet(ImportEH(decoder.EH, ehCount));
        //m_bDirty = false;

        return S_OK;
    }


    mdSignature ILRewriter::GetLocalsSignatureToken()
    {
        return m_tkLocalVarSig;
    }
    void ILRewriter::SetLocalsSignatureToken(mdSignature localVarSigToken)
    {
        m_tkLocalVarSig = localVarSigToken;;
    }

    SignatureInfo* ILRewriter::GetLocalsSignature()
    {
        if (_localsSignature == nullptr)
        {
            PCCOR_SIGNATURE pSig; DWORD nSig;
            auto module = m_methodInfo->GetModuleInfo();
            if (SUCCEEDED(module->GetMetaDataImport()->GetSigFromToken(m_tkLocalVarSig, &pSig, &nSig)))
            {
                _localsSignature = new SignatureInfo(module, pSig, nSig);
            }
        }
        return _localsSignature;

    }

    HRESULT ILRewriter::ImportIL(LPCBYTE pIL)
    {
        m_pOffsetToInstr = new ILInstr * [m_nCodeSize + 1];
        IfNullRet(m_pOffsetToInstr);

        ZeroMemory(m_pOffsetToInstr, m_nCodeSize * sizeof(ILInstr*));

        // Set the sentinel instruction
        m_pOffsetToInstr[m_nCodeSize] = &m_IL;
        m_IL.m_opcode = -1;

        bool fBranch = false;
        unsigned offset = 0;
        while (offset < m_nCodeSize)
        {
            unsigned startOffset = offset;
            unsigned opcode = pIL[offset++];

            if (opcode == CEE_PREFIX1)
            {
                if (offset >= m_nCodeSize)
                {
                    assert(false);
                    return COR_E_INVALIDPROGRAM;
                }
                opcode = 0x100 + pIL[offset++];
            }

            if ((CEE_PREFIX7 <= opcode) && (opcode <= CEE_PREFIX2))
            {
                // NOTE: CEE_PREFIX2-7 are currently not supported
                assert(false);
                return COR_E_INVALIDPROGRAM;
            }

            if (opcode >= CEE_COUNT)
            {
                assert(false);
                return COR_E_INVALIDPROGRAM;
            }

            BYTE flags = s_OpCodeFlags[opcode];

            int size = (flags & OPCODEFLAGS_SizeMask);
            if (offset + size > m_nCodeSize)
            {
                assert(false);
                return COR_E_INVALIDPROGRAM;
            }

            ILInstr* pInstr = NewILInstr();
            IfNullRet(pInstr);

            pInstr->m_opcode = opcode;
            pInstr->m_Arg64 = 0;

            InsertBefore(&m_IL, pInstr, false);

            m_pOffsetToInstr[startOffset] = pInstr;
            pInstr->m_offset = startOffset;

            switch (flags)
            {
            case 0:
                break;
            case 1:
                pInstr->m_Arg8 = *(UNALIGNED INT8*) & (pIL[offset]);
                break;
            case 2:
                pInstr->m_Arg16 = *(UNALIGNED INT16*) & (pIL[offset]);
                break;
            case 4:
                pInstr->m_Arg32 = *(UNALIGNED INT32*) & (pIL[offset]);
                break;
            case 8:
                pInstr->m_Arg64 = *(UNALIGNED INT64*) & (pIL[offset]);
                break;
            case 1 | OPCODEFLAGS_BranchTarget:
                pInstr->m_Arg32 = offset + 1 + *(UNALIGNED INT8*) & (pIL[offset]);
                fBranch = true;
                break;
            case 4 | OPCODEFLAGS_BranchTarget:
                pInstr->m_Arg32 = offset + 4 + *(UNALIGNED INT32*) & (pIL[offset]);
                fBranch = true;
                break;
            case 0 | OPCODEFLAGS_Switch:
            {
                if (offset + sizeof(INT32) > m_nCodeSize)
                {
                    assert(false);
                    return COR_E_INVALIDPROGRAM;
                }

                unsigned nTargets = *(UNALIGNED INT32*) & (pIL[offset]);
                pInstr->m_Arg32 = nTargets;
                offset += sizeof(INT32);

                unsigned base = offset + nTargets * sizeof(INT32);

                for (unsigned iTarget = 0; iTarget < nTargets; iTarget++)
                {
                    if (offset + sizeof(INT32) > m_nCodeSize)
                    {
                        assert(false);
                        return COR_E_INVALIDPROGRAM;
                    }

                    pInstr = NewILInstr();
                    IfNullRet(pInstr);

                    pInstr->m_opcode = CEE_SWITCH_ARG;

                    pInstr->m_Arg32 = base + *(UNALIGNED INT32*) & (pIL[offset]);
                    offset += sizeof(INT32);

                    InsertBefore(&m_IL, pInstr, false);
                }
                fBranch = true;
                break;
            }
            default:
                assert(false);
                break;
            }
            offset += size;

            //Simplify Macros
            if (opcode == CEE_LDARG_0 || opcode == CEE_LDLOC_0 || opcode == CEE_STLOC_0) { pInstr->m_Arg8 = 0; }
            else if (opcode == CEE_LDARG_1 || opcode == CEE_LDLOC_1 || opcode == CEE_STLOC_1) { pInstr->m_Arg8 = 1; }
            else if (opcode == CEE_LDARG_2 || opcode == CEE_LDLOC_2 || opcode == CEE_STLOC_2) { pInstr->m_Arg8 = 2; }
            else if (opcode == CEE_LDARG_3 || opcode == CEE_LDLOC_3 || opcode == CEE_STLOC_3) { pInstr->m_Arg8 = 3; }
        }
        assert(offset == m_nCodeSize);

        //if (fBranch)
        {
            // Go over all control flow instructions and resolve the targets
            for (ILInstr* pInstr = m_IL.m_pNext; pInstr != &m_IL; pInstr = pInstr->m_pNext)
            {
                if (s_OpCodeFlags[pInstr->m_opcode] & OPCODEFLAGS_BranchTarget)
                {
                    pInstr->m_pTarget = GetInstrFromOffset(pInstr->m_Arg32);
                }
                pInstr->m_originalArg64 = pInstr->m_Arg64; //Store original arg value
            }
        }

        return S_OK;
    }
    HRESULT ILRewriter::ImportEH(const COR_ILMETHOD_SECT_EH* pILEH, unsigned nEH)
    {
        assert(m_pEH == nullptr);

        m_nEH = nEH;

        if (nEH == 0)
        {
            return S_OK;
        }

        IfNullRet(m_pEH = new EHClause[m_nEH]);
        for (unsigned iEH = 0; iEH < m_nEH; iEH++)
        {
            // If the EH clause is in tiny form, the call to pILEH->EHClause() below will
            // use this as a scratch buffer to expand the EH clause into its fat form.
            COR_ILMETHOD_SECT_EH_CLAUSE_FAT scratch;

            const COR_ILMETHOD_SECT_EH_CLAUSE_FAT* ehInfo;
            ehInfo = (COR_ILMETHOD_SECT_EH_CLAUSE_FAT*)pILEH->EHClause(iEH, &scratch);

            EHClause* clause = &(m_pEH[iEH]);
            clause->m_Flags = ehInfo->GetFlags();

            clause->m_pTryBegin = GetInstrFromOffset(ehInfo->GetTryOffset());
            clause->m_pTryEnd = GetInstrFromOffset(ehInfo->GetTryOffset() + ehInfo->GetTryLength());
            clause->m_pHandlerBegin = GetInstrFromOffset(ehInfo->GetHandlerOffset());
            clause->m_pHandlerEnd = GetInstrFromOffset(ehInfo->GetHandlerOffset() + ehInfo->GetHandlerLength())->m_pPrev;
            if ((clause->m_Flags & COR_ILEXCEPTION_CLAUSE_FILTER) == 0)
            {
                clause->m_ClassToken = ehInfo->GetClassToken();
            }
            else
            {
                clause->m_pFilter = GetInstrFromOffset(ehInfo->GetFilterOffset());
            }
        }

        return S_OK;
    }
    void ILRewriter::ComputeOffsets()
    {
        unsigned offset = 0;
        for (ILInstr* pInstr = m_IL.m_pNext; pInstr != &m_IL; pInstr = pInstr->m_pNext)
        {
            unsigned opcode = pInstr->m_opcode;
            if (opcode < CEE_COUNT)
            {
                if (opcode >= 0x100) { offset++; }
                offset++;
            }

            pInstr->m_offset = offset;
            BYTE flags = s_OpCodeFlags[pInstr->m_opcode];
            if (flags == 0 || flags & OPCODEFLAGS_Switch)
            {
                offset += sizeof(INT32);
            }
            offset += (flags & OPCODEFLAGS_SizeMask);
        }
    }


    ILInstr* ILRewriter::NewILInstr(OPCODE opcode, ULONG32 arg, bool isNew)
    {
        //m_bDirty = true;
        m_nInstrs++;
        auto res = new ILInstr();
        res->m_offset = -1;
        res->m_opcode = opcode;
        res->m_Arg32 = arg;
        res->m_originalArg64 = res->m_Arg64;
        res->m_isNew = isNew;
        return res;
    }

    ILInstr* ILRewriter::GetInstrFromOffset(unsigned offset)
    {
        ILInstr* pInstr = nullptr;

        if (offset <= m_nCodeSize)
            pInstr = m_pOffsetToInstr[offset];

        assert(pInstr != nullptr);
        return pInstr;
    }

    std::vector<EHClause*> ILRewriter::GetExceptionHandlers()
    {
        std::vector<EHClause*> res;
        for (UINT x = 0; x < m_nEH; x++)
        {
            res.push_back(&(m_pEH[x]));
        }
        return res;
    }

    ILAnalysis* ILRewriter::StackAnalysis()
    {
        if (_stackAnalysis == nullptr)
        {
            _stackAnalysis = new ILAnalysis(this);
        }
        return _stackAnalysis;
    }

    ILInstr* ILRewriter::InsertBefore(ILInstr* pWhere, ILInstr* pWhat, bool updateReferences)
    {
        //m_bDirty = true;
        pWhat->m_pNext = pWhere;
        pWhat->m_pPrev = pWhere->m_pPrev;

        pWhat->m_pNext->m_pPrev = pWhat;
        pWhat->m_pPrev->m_pNext = pWhat;

        if (updateReferences)
        {
            // Go over all instructions and update references to pWhere instr
            for (ILInstr* pInstr = m_IL.m_pNext; pInstr != &m_IL; pInstr = pInstr->m_pNext)
            {
                if (pInstr->m_pTarget == pWhere)
                {
                    pInstr->m_pTarget = pWhat;
                }
            }

            //Update exception handlers references
            for (UINT x = 0; x < m_nEH; x++)
            {
                auto eh = &(m_pEH[x]);
                if (eh->m_pTryBegin == pWhere)
                {
                    eh->m_pTryBegin = pWhat;
                }
                if (eh->m_pHandlerBegin == pWhere)
                {
                    eh->m_pHandlerBegin = pWhat;
                }
                if (eh->m_pFilter == pWhere)
                {
                    eh->m_pFilter = pWhat;
                }
            }
        }

        AdjustState(pWhat);
        return pWhere;
    }
    ILInstr* ILRewriter::InsertAfter(ILInstr* pWhere, ILInstr* pWhat, bool updateReferences)
    {
        //m_bDirty = true;
        pWhat->m_pNext = pWhere->m_pNext;
        pWhat->m_pPrev = pWhere;

        pWhat->m_pNext->m_pPrev = pWhat;
        pWhat->m_pPrev->m_pNext = pWhat;

        if (updateReferences)
        {

            //Update exception handlers references
            for (UINT x = 0; x < m_nEH; x++)
            {
                auto eh = &(m_pEH[x]);
                if (eh->m_pTryEnd == pWhere) { eh->m_pTryEnd = pWhat; }
                if (eh->m_pHandlerEnd == pWhere) { eh->m_pHandlerEnd = pWhat; }
            }
        }

        AdjustState(pWhat);
        return pWhat;
    }

    void ILRewriter::AdjustState(ILInstr* pNewInstr)
    {
        m_maxStack += k_rgnStackPushes[pNewInstr->m_opcode];
    }

    ILInstr* ILRewriter::GetILList()
    {
        return &m_IL;
    }

    HRESULT ILRewriter::Export()
    {
        HRESULT hr = S_OK;

        if (!IsDirty())
        {
            return S_FALSE;
        }

        // One instruction produces 2 + sizeof(native int) bytes in the worst case which can be 10 bytes for 64-bit.
        // For simplification we just use 10 here.
        unsigned maxSize = m_nInstrs * 10;

        m_pOutputBuffer = new BYTE[maxSize];
        IfNullRet(m_pOutputBuffer);

    again:
        BYTE* pIL = m_pOutputBuffer;

        bool fBranch = false;
        unsigned offset = 0;

        // Go over all instructions and produce code for them
        for (ILInstr* pInstr = m_IL.m_pNext; pInstr != &m_IL; pInstr = pInstr->m_pNext)
        {
            assert(offset < maxSize);
            pInstr->m_offset = offset;

            unsigned opcode = pInstr->m_opcode;
            if (opcode < CEE_COUNT)
            {
                // CEE_PREFIX1 refers not to instruction prefixes (like tail.), but to
                // the lead byte of multi-byte opcodes. For now, the only lead byte
                // supported is CEE_PREFIX1 = 0xFE.
                if (opcode >= 0x100)
                    m_pOutputBuffer[offset++] = CEE_PREFIX1;

                // This appears to depend on an implicit conversion from
                // unsigned opcode down to BYTE, to deliberately lose data and have
                // opcode >= 0x100 wrap around to 0.
                m_pOutputBuffer[offset++] = (opcode & 0xFF);
            }

            assert(pInstr->m_opcode < _countof(s_OpCodeFlags));
            BYTE flags = s_OpCodeFlags[pInstr->m_opcode];
            switch (flags)
            {
            case 0:
                break;
            case 1:
                *(UNALIGNED INT8*) & (pIL[offset]) = pInstr->m_Arg8;
                break;
            case 2:
                *(UNALIGNED INT16*) & (pIL[offset]) = pInstr->m_Arg16;
                break;
            case 4:
                *(UNALIGNED INT32*) & (pIL[offset]) = pInstr->m_Arg32;
                break;
            case 8:
                *(UNALIGNED INT64*) & (pIL[offset]) = pInstr->m_Arg64;
                break;
            case 1 | OPCODEFLAGS_BranchTarget:
                fBranch = true;
                break;
            case 4 | OPCODEFLAGS_BranchTarget:
                fBranch = true;
                break;
            case 0 | OPCODEFLAGS_Switch:
                *(UNALIGNED INT32*) & (pIL[offset]) = pInstr->m_Arg32;
                offset += sizeof(INT32);
                break;
            default:
                assert(false);
                break;
            }
            offset += (flags & OPCODEFLAGS_SizeMask);
        }
        m_IL.m_offset = offset;

        if (fBranch)
        {
            bool fTryAgain = false;
            unsigned switchBase = 0;

            // Go over all control flow instructions and resolve the targets
            for (ILInstr* pInstr = m_IL.m_pNext; pInstr != &m_IL; pInstr = pInstr->m_pNext)
            {
                unsigned opcode = pInstr->m_opcode;

                if (pInstr->m_opcode == CEE_SWITCH)
                {
                    switchBase = pInstr->m_offset + 1 + sizeof(INT32) * (pInstr->m_Arg32 + 1);
                    continue;
                }
                if (opcode == CEE_SWITCH_ARG)
                {
                    // Switch args are special
                    *(UNALIGNED INT32*)& (pIL[pInstr->m_offset]) = pInstr->m_pTarget->m_offset - switchBase;
                    continue;
                }

                BYTE flags = s_OpCodeFlags[pInstr->m_opcode];

                if (flags & OPCODEFLAGS_BranchTarget)
                {
                    int delta = pInstr->m_pTarget->m_offset - pInstr->m_pNext->m_offset;

                    switch (flags)
                    {
                    case 1 | OPCODEFLAGS_BranchTarget:
                        // Check if delta is too big to fit into an INT8.
                        // 
                        // (see #pragma at top of file)
                        if ((INT8)delta != delta)
                        {
                            if (opcode == CEE_LEAVE_S)
                            {
                                pInstr->m_opcode = CEE_LEAVE;
                            }
                            else
                            {
                                assert(opcode >= CEE_BR_S && opcode <= CEE_BLT_UN_S);
                                pInstr->m_opcode = opcode - CEE_BR_S + CEE_BR;
                                assert(pInstr->m_opcode >= CEE_BR && pInstr->m_opcode <= CEE_BLT_UN);
                            }
                            fTryAgain = true;
                            continue;
                        }
                        *(UNALIGNED INT8*)& (pIL[pInstr->m_pNext->m_offset - sizeof(INT8)]) = delta;
                        break;
                    case 4 | OPCODEFLAGS_BranchTarget:
                        *(UNALIGNED INT32*) & (pIL[pInstr->m_pNext->m_offset - sizeof(INT32)]) = delta;
                        break;
                    default:
                        assert(false);
                        break;
                    }
                }
            }

            // Do the whole thing again if we changed the size of some branch targets
            if (fTryAgain)
                goto again;
        }

        unsigned codeSize = offset;
        unsigned totalSize;
        LPBYTE pBody = nullptr;
        if (m_fGenerateTinyHeader)
        {
            // Make sure we can fit in a tiny header
            if (codeSize >= 64)
                return E_FAIL;

            totalSize = sizeof(IMAGE_COR_ILMETHOD_TINY) + codeSize;
            pBody = new BYTE[totalSize];
            BYTE* pCurrent = pBody;

            // Here's the tiny header
            *pCurrent = (BYTE)(CorILMethod_TinyFormat | (codeSize << 2));
            pCurrent += sizeof(IMAGE_COR_ILMETHOD_TINY);

            // And the body
            CopyMemory(pCurrent, m_pOutputBuffer, codeSize);
        }
        else
        {
            // Use FAT header
            unsigned alignedCodeSize = (offset + 3) & ~3;

            totalSize = sizeof(IMAGE_COR_ILMETHOD_FAT) + alignedCodeSize +
                (m_nEH ? (sizeof(IMAGE_COR_ILMETHOD_SECT_FAT) + sizeof(IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT) * m_nEH) : 0);

            pBody = new BYTE[totalSize];
            BYTE* pCurrent = pBody;

            IMAGE_COR_ILMETHOD_FAT* pHeader = (IMAGE_COR_ILMETHOD_FAT*)pCurrent;
            pHeader->Flags = m_flags | (m_nEH ? CorILMethod_MoreSects : 0) | CorILMethod_FatFormat;
            pHeader->Size = sizeof(IMAGE_COR_ILMETHOD_FAT) / sizeof(DWORD);
            pHeader->MaxStack = m_maxStack;
            pHeader->CodeSize = offset;
            pHeader->LocalVarSigTok = m_tkLocalVarSig;

            pCurrent = (BYTE*)(pHeader + 1);

            CopyMemory(pCurrent, m_pOutputBuffer, codeSize);
            pCurrent += alignedCodeSize;

            if (m_nEH != 0)
            {
                IMAGE_COR_ILMETHOD_SECT_FAT* pEH = (IMAGE_COR_ILMETHOD_SECT_FAT*)pCurrent;
                pEH->Kind = CorILMethod_Sect_EHTable | CorILMethod_Sect_FatFormat;
                pEH->DataSize = (unsigned)(sizeof(IMAGE_COR_ILMETHOD_SECT_FAT) + sizeof(IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT) * m_nEH);

                pCurrent = (BYTE*)(pEH + 1);

                for (unsigned iEH = 0; iEH < m_nEH; iEH++)
                {
                    EHClause* pSrc = &(m_pEH[iEH]);
                    IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT* pDst = (IMAGE_COR_ILMETHOD_SECT_EH_CLAUSE_FAT*)pCurrent;

                    pDst->Flags = pSrc->m_Flags;
                    pDst->TryOffset = pSrc->m_pTryBegin->m_offset;
                    pDst->TryLength = pSrc->m_pTryEnd->m_offset - pSrc->m_pTryBegin->m_offset;
                    pDst->HandlerOffset = pSrc->m_pHandlerBegin->m_offset;
                    pDst->HandlerLength = pSrc->m_pHandlerEnd->m_pNext->m_offset - pSrc->m_pHandlerBegin->m_offset;
                    if ((pSrc->m_Flags & COR_ILEXCEPTION_CLAUSE_FILTER) == 0)
                    {
                        pDst->ClassToken = pSrc->m_ClassToken;
                    }
                    else
                    {
                        pDst->FilterOffset = pSrc->m_pFilter->m_offset;
                    }

                    pCurrent = (BYTE*)(pDst + 1);
                }
            }
        }

        //SetMethodIL keeps the buffer -> no need to dealocate it
        IfFailRet(m_methodInfo->SetMethodIL(totalSize, pBody));

        return hr;
    }

    HRESULT ILRewriter::AddProbeBefore(mdToken hookMethod, ILInstr* pInsertProbeBeforeThisInstr, OPCODE opCode, ULONG32* hookArg)
    {
        ILInstr* pNewInstr = nullptr;

        if (hookArg != nullptr)
        {
            pNewInstr = NewILInstr();
            pNewInstr->m_opcode = opCode;// CEE_LDC_I4;
            pNewInstr->m_Arg32 = *hookArg;
            InsertBefore(pInsertProbeBeforeThisInstr, pNewInstr);
        }

        pNewInstr = NewILInstr();
        pNewInstr->m_opcode = CEE_CALL;
        pNewInstr->m_Arg32 = hookMethod;
        InsertBefore(pInsertProbeBeforeThisInstr, pNewInstr);

        return S_OK;
    }
    HRESULT ILRewriter::AddProbeAfter(mdToken hookMethod, ILInstr* pInsertProbeAfterThisInstr, OPCODE opCode, ULONG32* hookArg)
    {
        ILInstr* pNewInstr = nullptr;

        pNewInstr = NewILInstr();
        pNewInstr->m_opcode = CEE_CALL;
        pNewInstr->m_Arg32 = hookMethod;
        InsertAfter(pInsertProbeAfterThisInstr, pNewInstr);

        if (hookArg != nullptr)
        {
            pNewInstr = NewILInstr();
            pNewInstr->m_opcode = opCode;// CEE_LDC_I4;
            pNewInstr->m_Arg32 = *hookArg;
            InsertAfter(pInsertProbeAfterThisInstr, pNewInstr);
        }

        return S_OK;
    }

    HRESULT ILRewriter::AddEnterProbe(mdToken onEnterHook, OPCODE opCode, ULONG32* hookArg)
    {
        ILInstr* pFirstOriginalInstr = GetILList()->m_pNext;
        return AddProbeBefore(onEnterHook, pFirstOriginalInstr, opCode, hookArg);
    }
    HRESULT ILRewriter::AddExitProbe(mdToken onExitHook, OPCODE opCode, ULONG32* hookArg)
    {
        HRESULT hr = S_OK;
        BOOL fAtLeastOneProbeAdded = FALSE;

        // Find all RETs, and insert a call to the exit probe before each one.
        for (ILInstr* pInstr = GetILList()->m_pNext; pInstr != GetILList(); pInstr = pInstr->m_pNext)
        {
            switch (pInstr->m_opcode)
            {
            case CEE_RET:
            {
                // We want any branches or leaves that targeted the RET instruction to
                // actually target the epilog instructions we're adding. So turn the "RET"
                // into ["NOP", "RET"], and THEN add the epilog between the NOP & RET. That
                // ensures that any branches that went to the RET will now go to the NOP and
                // then execute our epilog.

                // NOTE: The NOP is not strictly required, but is a simplification of the implementation.
                // RET->NOP
                pInstr->m_opcode = CEE_NOP;

                // Add the new RET after
                ILInstr* pNewRet = NewILInstr();
                pNewRet->m_opcode = CEE_RET;
                InsertAfter(pInstr, pNewRet);

                // Add now insert the epilog before the new RET
                hr = AddProbeBefore(onExitHook, pNewRet, opCode, hookArg);
                if (FAILED(hr))
                    return hr;
                fAtLeastOneProbeAdded = TRUE;

                // Advance pInstr after all this gunk so the for loop continues properly
                pInstr = pNewRet;
                break;
            }

            default:
                break;
            }
        }

        if (!fAtLeastOneProbeAdded)
            return E_FAIL;

        return S_OK;
    }
}
