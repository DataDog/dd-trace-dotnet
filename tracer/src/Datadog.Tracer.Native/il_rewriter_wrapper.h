#ifndef DD_CLR_PROFILER_IL_REWRITER_WRAPPER_H_
#define DD_CLR_PROFILER_IL_REWRITER_WRAPPER_H_

#include "il_rewriter.h"
#include "module_metadata.h"

class ILRewriterWrapper
{
private:
    ILRewriter* const m_ILRewriter;
    ILInstr* m_ILInstr;

public:
    ILRewriterWrapper(ILRewriter* const il_rewriter) : m_ILRewriter(il_rewriter), m_ILInstr(nullptr)
    {
    }

    ILRewriter* GetILRewriter() const;
    ILInstr* GetCurrentILInstr() const;
    void SetILPosition(ILInstr* pILInstr);
    void Pop() const;
    ILInstr* LoadNull() const;
    void LoadInt64(INT64 value) const;
    void LoadInt32(INT32 value) const;
    ILInstr* LoadArgument(UINT16 index) const;
    ILInstr* LoadArgumentRef(UINT16 index) const;
    void LoadFieldAddress(const mdFieldDef field_def) const;
    void Cast(mdTypeRef type_ref) const;
    void Box(mdTypeRef type_ref) const;
    void UnboxAny(mdTypeRef type_ref) const;
    void UnboxAnyAfter(mdTypeRef type_ref) const;
    void CreateArray(mdTypeRef type_ref, INT32 size) const;
    ILInstr* CallMember(const mdMemberRef& member_ref, bool is_virtual) const;
    void Duplicate() const;
    void BeginLoadValueIntoArray(INT32 arrayIndex) const;
    void EndLoadValueIntoArray() const;
    bool ReplaceMethodCalls(mdMemberRef old_method_ref, mdMemberRef new_method_ref) const;
    ILInstr* LoadToken(mdToken token) const;
    ILInstr* LoadObj(mdToken token) const;
    ILInstr* LoadStr(mdString token) const;
    ILInstr* StLocal(unsigned index) const;
    ILInstr* LoadLocal(unsigned index) const;
    ILInstr* LoadLocalAddress(unsigned index) const;
    ILInstr* Return() const;
    ILInstr* NOP() const;
    ILInstr* Rethrow() const;
    ILInstr* EndFinally() const;

    ILInstr* CreateInstr(unsigned opCode) const;
    ILInstr* InitObj(mdTypeRef type_ref) const;
};

#endif // DD_CLR_PROFILER_IL_REWRITER_WRAPPER_H_
