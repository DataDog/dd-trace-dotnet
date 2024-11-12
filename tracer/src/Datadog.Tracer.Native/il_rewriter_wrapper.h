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
    ILInstr* Pop() const;
    ILInstr* LoadNull() const;
    ILInstr* LoadInt64(INT64 value) const;
    ILInstr* LoadInt32(INT32 value) const;
    ILInstr* LoadArgument(UINT16 index) const;
    ILInstr* LoadArgumentRef(UINT16 index) const;
    ILInstr* LoadFieldAddress(mdFieldDef field_def, bool isStatic = false) const;
    ILInstr* Cast(mdTypeRef type_ref) const;
    ILInstr* Box(mdTypeRef type_ref) const;
    ILInstr* UnboxAny(mdTypeRef type_ref) const;
    ILInstr* UnboxAnyAfter(mdTypeRef type_ref) const;
    void CreateArray(mdTypeRef type_ref, INT32 size) const;
    ILInstr* CallMember(const mdMemberRef& member_ref, bool is_virtual) const;
    ILInstr* Duplicate() const;
    void BeginLoadValueIntoArray(INT32 arrayIndex) const;
    ILInstr* EndLoadValueIntoArray() const;
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
    ILInstr* CreateInstr(unsigned opCode, uint32_t arg) const;
    ILInstr* CreateInstr(unsigned opCode, ILInstr* target) const;
    ILInstr* InitObj(mdTypeRef type_ref) const;
};

#endif // DD_CLR_PROFILER_IL_REWRITER_WRAPPER_H_
