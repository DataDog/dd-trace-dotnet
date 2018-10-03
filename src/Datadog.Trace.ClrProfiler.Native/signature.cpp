#include "signature.h"

namespace trace::signature {

std::optional<CorCallingConvention> Reader::ReadCallingConvention() {
  DWORD convention = 0;
  auto hr = CorSigUncompressCallingConv(
      data_.data(), (DWORD)data_.size() - pos_, &convention);
  if (FAILED(hr)) {
    return {};
  }
  pos_++;
  return (CorCallingConvention)convention;
}

std::shared_ptr<Type> Reader::ReadType() {
  if (pos_ >= data_.size()) {
    return nullptr;
  }

  CorElementType type_name = CorElementType(data_[pos_++]);
  Type type(type_name);
  switch (type_name) {
    case ELEMENT_TYPE_BOOLEAN:
    case ELEMENT_TYPE_CHAR:
    case ELEMENT_TYPE_I1:
    case ELEMENT_TYPE_U1:
    case ELEMENT_TYPE_U2:
    case ELEMENT_TYPE_I2:
    case ELEMENT_TYPE_I4:
    case ELEMENT_TYPE_U4:
    case ELEMENT_TYPE_I8:
    case ELEMENT_TYPE_U8:
    case ELEMENT_TYPE_R4:
    case ELEMENT_TYPE_R8:
    case ELEMENT_TYPE_I:
    case ELEMENT_TYPE_U:
    case ELEMENT_TYPE_STRING:
    case ELEMENT_TYPE_OBJECT:
    case ELEMENT_TYPE_TYPEDBYREF:
    case ELEMENT_TYPE_VOID:
      return std::make_shared<Type>(type_name);
    case ELEMENT_TYPE_BYREF:
    case ELEMENT_TYPE_PTR:
    case ELEMENT_TYPE_SZARRAY: {
      auto child_type = ReadType();
      if (child_type == nullptr) {
        return nullptr;
      }
      return std::make_shared<Type>(type_name, child_type);
    } break;
    case ELEMENT_TYPE_CLASS:
    case ELEMENT_TYPE_VALUETYPE: {
      auto token = ReadToken();
      if (!token.has_value()) {
        return nullptr;
      }
      return std::make_shared<Type>(type_name, token.value());
    } break;
    case ELEMENT_TYPE_FNPTR: {
      return nullptr;
      // auto method = ParseMethod();
      // type = std::make_shared<FnptrType>(method);
    } break;
    case ELEMENT_TYPE_ARRAY: {
      return nullptr;
      // type = ReadArray();
    } break;
    case ELEMENT_TYPE_GENERICINST: {
      return nullptr;
      /*auto instType =
      std::dynamic_pointer_cast<ClassValueBase>(ReadType()); if (instType ==
      nullptr) { THROW("Error: expected a class or value type");
      }
      auto nTypes = ReadUInt();
      std::list<std::shared_ptr<Type>> typesList;
      for (int i = 0; i < nTypes; i++) {
        std::cout << i << std::endl;
        typesList.push_back(ReadType());
      }
      type = std::make_shared<GenericInstanceType>(instType, typesList);*/
    } break;
    case ELEMENT_TYPE_VAR:
    case ELEMENT_TYPE_MVAR: {
      auto sz = ReadUInt();
      if (sz.has_value()) {
        return std::make_shared<Type>(type_name, sz.value());
      }
    } break;
  }
  return nullptr;
}

std::shared_ptr<Local> Reader::ReadLocal() {
  auto type = ReadType();
  if (type == nullptr) {
    return nullptr;
  }
  return std::make_shared<Local>(type);
}

std::shared_ptr<Locals> Reader::ReadLocals() {
  auto calling_convention = ReadCallingConvention();
  if (!calling_convention.has_value()) {
    return nullptr;
  }

  auto count = ReadUInt();
  if (!count.has_value()) {
    return nullptr;
  }

  std::vector<std::shared_ptr<Local>> locals = {};
  for (ULONG i = 0; i < count; i++) {
    auto local = ReadLocal();
    if (local == nullptr) {
      break;
    }
    locals.push_back(local);
  }
  return std::make_shared<Locals>(locals);
}

std::optional<mdToken> Reader::ReadToken() {
  mdToken token = mdTokenNil;
  DWORD token_length;
  auto hr = CorSigUncompressToken(data_.data(), (DWORD)data_.size() - pos_,
                                  &token, &token_length);
  if (FAILED(hr) || token == mdTokenNil) {
    return {};
  }
  pos_ += token_length;
  return token;
}

std::optional<ULONG> Reader::ReadUInt() {
  ULONG value = 0;
  DWORD token_length;
  auto hr = CorSigUncompressData(data_.data(), (DWORD)data_.size() - pos_,
                                 &value, &token_length);
  if (FAILED(hr)) {
    return {};
  }
  pos_ += token_length;
  return value;
}

void Writer::WriteToken(mdToken token) {
  std::array<uint8_t, 4> buf;
  auto sz = CorSigCompressToken(token, buf.data());
  if (sz > 0) {
    data.insert(data.end(), buf.data(), buf.data() + sz);
  }
}

void Writer::WriteUInt(ULONG value) {
  std::array<uint8_t, 4> buf;
  auto sz = CorSigCompressData(value, buf.data());
  if (sz > 0) {
    data.insert(data.end(), buf.data(), buf.data() + sz);
  }
}

}  // namespace trace::signature