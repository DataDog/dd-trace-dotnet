#pragma once

#include "ILRewriter.h"
#include "ModuleMetadata.h"

class ILRewriterWrapper {
 private:
  ILRewriter* const m_ILRewriter;
  ILInstr* m_ILInstr;

 public:
  ILRewriterWrapper(ILRewriter* const il_rewriter)
      : m_ILRewriter(il_rewriter), m_ILInstr(nullptr) {}

  ILRewriter* GetILRewriter() const;
  void SetILPosition(ILInstr* pILInstr);
  void Pop() const;
  void LoadNull() const;
  void LoadInt64(INT64 value) const;
  void LoadInt32(INT32 value) const;
  void LoadArgument(UINT16 index) const;
  void Cast(mdTypeRef type_ref) const;
  void Box(mdTypeRef type_ref) const;
  void UnboxAny(mdTypeRef type_ref) const;
  void CreateArray(mdTypeRef type_ref, INT32 size) const;
  void CallMember(const mdMemberRef& member_ref, bool is_virtual) const;
  void Duplicate() const;
  void BeginLoadValueIntoArray(INT32 arrayIndex) const;
  void EndLoadValueIntoArray() const;
  bool ReplaceMethodCalls(mdMemberRef old_method_ref,
                          mdMemberRef new_method_ref) const;
};
