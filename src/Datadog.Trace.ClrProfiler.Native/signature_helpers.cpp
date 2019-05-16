#include "signature_helpers.h"

#include <array>
#include <comdef.h>
#include <wrl.h>
#include "clr_helpers.h"

#include <cstring>

#include "logging.h"
#include "macros.h"

namespace trace {
using namespace std;

WSTRING getTypeName(const ComPtr<IMetaDataImport2>& metadata, mdToken token) {
  identifier nameData;
  ULONG nameLength = 0;
  HRESULT hr = E_FAIL;
  switch (TypeFromToken(token)) {
    case mdtTypeDef:
      hr = metadata->GetTypeDefProps(token, nameData.data(), nameData.size(),
                                     &nameLength, nullptr, nullptr);
      break;

    case mdtTypeRef:
      hr = metadata->GetTypeRefProps(token, nullptr, nameData.data(),
                                     nameData.size(), &nameLength);
      break;

    default:
      break;
  }

  if (FAILED(hr)) {
    return ""_W;
  }
  return {nameData.data(), nameLength - 1};
}

PCCOR_SIGNATURE consumeType(PCCOR_SIGNATURE& signature) {
  const PCCOR_SIGNATURE start = signature;

  const CorElementType elementType = CorSigUncompressElementType(signature);
  switch (elementType) {
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
      return start;

    case ELEMENT_TYPE_VALUETYPE:
      CorSigUncompressToken(signature);
      return start;

    case ELEMENT_TYPE_CLASS:
      CorSigUncompressToken(signature);
      return start;

    case ELEMENT_TYPE_OBJECT:
      return start;

    case ELEMENT_TYPE_SZARRAY:
      consumeType(signature);
      return start;

    case ELEMENT_TYPE_VAR:
      CorSigUncompressData(signature);
      return start;

    case ELEMENT_TYPE_GENERICINST: {
      CorSigUncompressElementType(signature);
      CorSigUncompressToken(signature);

      const ULONG genericArgumentsCount = CorSigUncompressData(signature);
      for (size_t i = 0; i < genericArgumentsCount; ++i) {
        consumeType(signature);
      }

      return start;
    }

    case ELEMENT_TYPE_BYREF:
      consumeType(signature);
      return start;

    default:
      return start;  // TODO: WHAT EVEN HAPPENS
  }
}

void SignatureToWSTRING(const ComPtr<IMetaDataImport2>& metadata,
                         PCCOR_SIGNATURE signature, WSTRING& result) {
  const CorElementType elementType = CorSigUncompressElementType(signature);
  switch (elementType) {
    case ELEMENT_TYPE_VOID:
      result += L"Void";
      break;

    case ELEMENT_TYPE_BOOLEAN:
      result += L"Boolean";
      break;

    case ELEMENT_TYPE_CHAR:
      result += L"Char16";
      break;

    case ELEMENT_TYPE_I1:
      result += L"Int8";
      break;

    case ELEMENT_TYPE_U1:
      result += L"UInt8";
      break;

    case ELEMENT_TYPE_I2:
      result += L"Int16";
      break;

    case ELEMENT_TYPE_U2:
      result += L"UInt16";
      break;

    case ELEMENT_TYPE_I4:
      result += L"Int32";
      break;

    case ELEMENT_TYPE_U4:
      result += L"UInt32";
      break;

    case ELEMENT_TYPE_I8:
      result += L"Int64";
      break;

    case ELEMENT_TYPE_U8:
      result += L"UInt64";
      break;

    case ELEMENT_TYPE_R4:
      result += L"Single";
      break;

    case ELEMENT_TYPE_R8:
      result += L"Double";
      break;

    case ELEMENT_TYPE_STRING:
      result += L"String";
      break;

    case ELEMENT_TYPE_VALUETYPE: {
      const mdToken token = CorSigUncompressToken(signature);
      const WSTRING className = getTypeName(metadata, token);
      if (className == L"System.Guid") {
        result += L"Guid";
      } else {
        result += className;
      }
      break;
    }

    case ELEMENT_TYPE_CLASS: {
      const mdToken token = CorSigUncompressToken(signature);
      result += getTypeName(metadata, token);
      break;
    }

    case ELEMENT_TYPE_OBJECT:
      result += L"Object";
      break;

    case ELEMENT_TYPE_SZARRAY:
      SignatureToWSTRING(metadata, signature, result);
      result += L"[]";
      break;

    case ELEMENT_TYPE_VAR: {
      const ULONG index = CorSigUncompressData(signature);
      result += L"Var!";
      result += ToWSTRING(index);
      break;
    }

    case ELEMENT_TYPE_GENERICINST: {
      const CorElementType genericType = CorSigUncompressElementType(signature);
      if (genericType != ELEMENT_TYPE_CLASS) {
        // Unexpected, let's drop out
        break;
      }

      const mdToken token = CorSigUncompressToken(signature);
      result += getTypeName(metadata, token);

      result += L'<';

      const ULONG genericArgumentsCount = CorSigUncompressData(signature);
      for (size_t i = 0; i < genericArgumentsCount; ++i) {
        PCCOR_SIGNATURE type = consumeType(signature);
        SignatureToWSTRING(metadata, type, result);

        if (i != genericArgumentsCount - 1) {
          result += L", ";
        }
      }

      result += L'>';
      break;
    }

    case ELEMENT_TYPE_BYREF:
      result += L"ByRef ";
      SignatureToWSTRING(metadata, signature, result);
      break;

    default:
      // We couldn't figure out the type to inspect
      break;
  }
}

WSTRING SignatureToWSTRING(const ComPtr<IMetaDataImport2>& metadata,
                            PCCOR_SIGNATURE signature) {
  WSTRING result;
  SignatureToWSTRING(metadata, signature, result);
  return result;
}
}  // namespace trace
