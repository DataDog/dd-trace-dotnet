#include "sig_helpers.h"

namespace trace {

bool ParseNumber(PCCOR_SIGNATURE* p_sig, ULONG* number) {
  ULONG result = CorSigUncompressData(*p_sig, number);
  if (result == -1) {
    return false;
  }

  *p_sig += result;
  return true;
}

bool ParseMethod(PCCOR_SIGNATURE* p_sig) {
  // Format:  [[HASTHIS] [EXPLICITTHIS]] (DEFAULT|VARARG|GENERIC GenParamCount)
  //                    ParamCount RetType Param* [SENTINEL Param+]
  return true;  // TODO: Implement
}

bool ParseArrayShape(PCCOR_SIGNATURE* p_sig) {
  // Format: Rank NumSizes Size* NumLoBounds LoBound*
  ULONG rank = 0, numsizes = 0, size = 0;
  if (!ParseNumber(p_sig, &rank) || !ParseNumber(p_sig, &numsizes)) {
    return false;
  }

  for (ULONG i = 0; i < numsizes; i++) {
    if (!ParseNumber(p_sig, &size)) {
      return false;
    }
  }

  if (!ParseNumber(p_sig, &numsizes)) {
    return false;
  }

  for (ULONG i = 0; i < numsizes; i++) {
    if (!ParseNumber(p_sig, &size)) {
      return false;
    }
  }

  return true;
}

bool ParseTypeDefOrRefEncoded(PCCOR_SIGNATURE* p_sig) {
  mdToken type_token;
  ULONG result;
  result = CorSigUncompressToken(*p_sig, &type_token);
  if (result == -1) {
    return false;
  }

  *p_sig += result;
  return true;
}

bool ParseCustomMod(PCCOR_SIGNATURE* p_sig) {
  if (**p_sig == ELEMENT_TYPE_CMOD_OPT || **p_sig == ELEMENT_TYPE_CMOD_REQD) {
    *p_sig += 1;
    return ParseTypeDefOrRefEncoded(p_sig);
  }

  return false;
}

bool ParseOptionalCustomMods(PCCOR_SIGNATURE* p_sig) {
  for (;;) {
    switch (**p_sig) {
      case ELEMENT_TYPE_CMOD_OPT:
      case ELEMENT_TYPE_CMOD_REQD:
        if (!ParseCustomMod(p_sig)) {
          return false;
        }
        break;
      default:
        return true;
    }
  }

  return false;
}

// Returns whether or not the Type signature at the given address could be parsed.
// If successful, the input pointer will point to the next byte following the Type signature.
// If not, the input pointer may point to invalid data.
bool ParseType(PCCOR_SIGNATURE* p_sig) {
  /*
  Format = BOOLEAN | CHAR | I1 | U1 | U2 | U2 | I4 | U4 | I8 | U8 | R4 | R8 | I | U | STRING | OBJECT
               | VALUETYPE TypeDefOrRefEncoded
               | CLASS TypeDefOrRefEncoded
               | PTR CustomMod* VOID
               | PTR CustomMod* Type
               | FNPTR MethodDefSig
               | FNPTR MethodRefSig
               | ARRAY Type ArrayShape
               | SZARRAY CustomMod* Type
               | GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type *
               | VAR Number
               | MVAR Number
  */

  const auto cor_element_type = CorElementType(**p_sig);
  ULONG number = 0;
  *p_sig += 1;

  switch (cor_element_type) {
    case ELEMENT_TYPE_VOID:
    case ELEMENT_TYPE_BOOLEAN:
    case ELEMENT_TYPE_CHAR:
    case ELEMENT_TYPE_I1:
    case ELEMENT_TYPE_U1:
    case ELEMENT_TYPE_I2:
    case ELEMENT_TYPE_U2:
    case ELEMENT_TYPE_I4:
    case ELEMENT_TYPE_U4:
    case ELEMENT_TYPE_I8:
    case ELEMENT_TYPE_U8:
    case ELEMENT_TYPE_R4:
    case ELEMENT_TYPE_R8:
    case ELEMENT_TYPE_STRING:
    case ELEMENT_TYPE_OBJECT:
      return true;

    case ELEMENT_TYPE_PTR:
      // Format: PTR CustomMod* VOID
      // Format: PTR CustomMod* Type
      if (!ParseOptionalCustomMods(p_sig)) {
        return false;
      }

      if (**p_sig == ELEMENT_TYPE_VOID) {
        *p_sig += 1;
        return true;
      } else {
        return ParseType(p_sig);
      }

    case ELEMENT_TYPE_VALUETYPE:
    case ELEMENT_TYPE_CLASS:
      // Format: CLASS TypeDefOrRefEncoded
      // Format: VALUETYPE TypeDefOrRefEncoded
      return ParseTypeDefOrRefEncoded(p_sig);

    case ELEMENT_TYPE_FNPTR:
      // Format: FNPTR MethodDefSig
      // Format: FNPTR MethodRefSig
      return ParseMethod(p_sig);

    case ELEMENT_TYPE_ARRAY:
      // Format: ARRAY Type ArrayShape
      if (!ParseType(p_sig)) {
        return false;
      }
      return ParseArrayShape(p_sig);

    case ELEMENT_TYPE_SZARRAY:
      // Format: SZARRAY CustomMod* Type
      if (!ParseOptionalCustomMods(p_sig)) {
        return false;
      }
      return ParseType(p_sig);

    case ELEMENT_TYPE_GENERICINST:
      if (**p_sig != ELEMENT_TYPE_VALUETYPE && **p_sig != ELEMENT_TYPE_CLASS) {
        return false;
      }

      *p_sig += 1;
      if (!ParseTypeDefOrRefEncoded(p_sig)) {
        return false;
      }

      if (!ParseNumber(p_sig, &number)) {
        return false;
      }

      for (ULONG i = 0; i < number; i++) {
        if (!ParseType(p_sig)) {
          return false;
        }
      }
      return true;

    case ELEMENT_TYPE_VAR:
    case ELEMENT_TYPE_MVAR:
      // Format: VAR Number
      // Format: MVAR Number
      return ParseNumber(p_sig, &number);

    default:
      return false;
  }
}
}