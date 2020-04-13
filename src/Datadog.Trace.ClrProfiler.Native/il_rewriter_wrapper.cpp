#include "il_rewriter_wrapper.h"

ILRewriter* ILRewriterWrapper::GetILRewriter() const { return m_ILRewriter; }

void ILRewriterWrapper::SetILPosition(ILInstr* pILInstr) {
  m_ILInstr = pILInstr;
}

void ILRewriterWrapper::Pop() const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_POP;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::LoadNull() const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_LDNULL;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::LoadInt64(const INT64 value) const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_LDC_I8;
  pNewInstr->m_Arg64 = value;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::LoadInt32(const INT32 value) const {
  static const std::vector<OPCODE> opcodes = {
      CEE_LDC_I4_0, CEE_LDC_I4_1, CEE_LDC_I4_2, CEE_LDC_I4_3, CEE_LDC_I4_4,
      CEE_LDC_I4_5, CEE_LDC_I4_6, CEE_LDC_I4_7, CEE_LDC_I4_8,
  };

  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();

  if (value >= 0 && value <= 8) {
    pNewInstr->m_opcode = opcodes[value];
  } else if (-128 <= value && value <= 127) {
    pNewInstr->m_opcode = CEE_LDC_I4_S;
    pNewInstr->m_Arg8 = static_cast<INT8>(value);
  } else {
    pNewInstr->m_opcode = CEE_LDC_I4;
    pNewInstr->m_Arg32 = value;
  }

  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::LoadArgument(const UINT16 index) const {
  static const std::vector<OPCODE> opcodes = {
      CEE_LDARG_0,
      CEE_LDARG_1,
      CEE_LDARG_2,
      CEE_LDARG_3,
  };

  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();

  if (index >= 0 && index <= 3) {
    pNewInstr->m_opcode = opcodes[index];
  } else if (index <= 255) {
    pNewInstr->m_opcode = CEE_LDARG_S;
    pNewInstr->m_Arg8 = static_cast<UINT8>(index);
  } else {
    pNewInstr->m_opcode = CEE_LDARG;
    pNewInstr->m_Arg16 = index;
  }

  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::Cast(const mdTypeRef type_ref) const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_CASTCLASS;
  pNewInstr->m_Arg32 = type_ref;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::Box(const mdTypeRef type_ref) const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_BOX;
  pNewInstr->m_Arg32 = type_ref;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::UnboxAny(const mdTypeRef type_ref) const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_UNBOX_ANY;
  pNewInstr->m_Arg32 = type_ref;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::UnboxAnyAfter(const mdTypeRef type_ref) const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_UNBOX_ANY;
  pNewInstr->m_Arg32 = type_ref;
  m_ILRewriter->InsertAfter(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::CreateArray(const mdTypeRef type_ref,
                                    const INT32 size) const {
  mdTypeRef typeRef = mdTypeRefNil;
  LoadInt32(size);

  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_NEWARR;
  pNewInstr->m_Arg32 = type_ref;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::CallMember(const mdMemberRef& member_ref,
                                   const bool is_virtual) const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = is_virtual ? CEE_CALLVIRT : CEE_CALL;
  pNewInstr->m_Arg32 = member_ref;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::Duplicate() const {
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_DUP;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

void ILRewriterWrapper::BeginLoadValueIntoArray(const INT32 arrayIndex) const {
  // duplicate the array reference
  Duplicate();

  // load the specified array index
  LoadInt32(arrayIndex);
}

void ILRewriterWrapper::EndLoadValueIntoArray() const {
  // stelem.ref (store value into array at the specified index)
  ILInstr* pNewInstr = m_ILRewriter->NewILInstr();
  pNewInstr->m_opcode = CEE_STELEM_REF;
  m_ILRewriter->InsertBefore(m_ILInstr, pNewInstr);
}

bool ILRewriterWrapper::ReplaceMethodCalls(
    const mdMemberRef old_method_ref, const mdMemberRef new_method_ref) const {
  bool modified = false;

  for (ILInstr* pInstr = m_ILRewriter->GetILList()->m_pNext;
       pInstr != m_ILRewriter->GetILList(); pInstr = pInstr->m_pNext) {
    if ((pInstr->m_opcode == CEE_CALL || pInstr->m_opcode == CEE_CALLVIRT) &&
        pInstr->m_Arg32 == static_cast<INT32>(old_method_ref)) {
      pInstr->m_opcode = CEE_CALL;
      pInstr->m_Arg32 = new_method_ref;

      modified = true;
    }
  }

  return modified;
}
