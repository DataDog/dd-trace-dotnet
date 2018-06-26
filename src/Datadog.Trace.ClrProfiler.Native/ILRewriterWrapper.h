#pragma once

#include "ILRewriter.h"
#include "ModuleMetadata.h"

class ILRewriterWrapper
{
private:
    ILRewriter* const m_ILRewriter;
    ILInstr* m_ILInstr;
    const ModuleMetadata& m_metadataHelper;

public:

    ILRewriterWrapper(ILRewriter* const il_rewriter, const ModuleMetadata& metadata_helper)
        : m_ILRewriter(il_rewriter),
          m_ILInstr(nullptr),
          m_metadataHelper(metadata_helper)
    {
    }

    ILRewriter* GetILRewriter() const;
    void SetILPosition(ILInstr* pILInstr);
    void Pop() const;
    void LoadNull() const;
    void LoadInt64(INT64 value) const;
    void LoadInt32(INT32 value) const;
    void LoadArgument(UINT16 index) const;
    void Cast(const TypeReference& type) const;
    void Box(const TypeReference& type) const;
    void UnboxAny(const TypeReference& type) const;
    void CreateArray(const TypeReference& type, const INT32 size) const;
    void CallMember(const MemberReference& member) const;
    void Duplicate() const;
    void BeginLoadValueIntoArray(INT32 arrayIndex) const;
    void EndLoadValueIntoArray() const;
};
