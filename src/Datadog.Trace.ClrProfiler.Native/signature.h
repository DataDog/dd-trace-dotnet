#ifndef _SIGNATURE_PARSER_H_
#define _SIGNATURE_PARSER_H_

#include "cor.h"
#include "corprof.h"
#include "util.h"

#include <array>
#include <memory>
#include <optional>
#include <vector>

namespace trace::signature {

struct Type {
 public:
  const CorElementType element_type;
  const std::shared_ptr<Type> child_type;
  const mdToken token;
  const ULONG generic_position;

  Type(const CorElementType element_type)
      : element_type(element_type),
        child_type(nullptr),
        token(mdTokenNil),
        generic_position(0) {}
  Type(const CorElementType element_type,
       const std::shared_ptr<Type>& child_type)
      : element_type(element_type),
        child_type(child_type),
        token(mdTokenNil),
        generic_position(0) {}
  Type(const CorElementType element_type, const mdToken token)
      : element_type(element_type),
        child_type(nullptr),
        token(token),
        generic_position(0) {}
  Type(const CorElementType element_type, const ULONG generic_position)
      : element_type(element_type),
        child_type(nullptr),
        token(mdTokenNil),
        generic_position(generic_position) {}
};

struct Local {
 public:
  const std::shared_ptr<Type> type;

  Local(const std::shared_ptr<Type>& type) : type(type) {}
};

struct Locals {
 public:
  const std::vector<std::shared_ptr<Local>> locals;

  Locals(const std::vector<std::shared_ptr<Local>>& locals) : locals(locals) {}
};

class Reader {
 private:
  const std::vector<uint8_t> data_;
  size_t pos_;

 public:
  Reader(std::shared_ptr<COR_SIGNATURE> data, ULONG length)
      : data_(data.get(), data.get() + length), pos_(0) {}
  Reader(PCCOR_SIGNATURE data, ULONG length)
      : data_(data, data + length), pos_(0) {}

  std::shared_ptr<Locals> ReadLocals();
  std::shared_ptr<Local> ReadLocal();
  std::shared_ptr<Type> ReadType();

  std::optional<CorCallingConvention> ReadCallingConvention();
  std::optional<mdToken> ReadToken();
  std::optional<ULONG> ReadUInt();
};

class Writer {
 public:
  std::vector<uint8_t> data;

  Writer() : data({}) {}

  void WriteLocals(std::shared_ptr<Locals> locals);
  void WriteLocal(std::shared_ptr<Local> local);
  void WriteType(std::shared_ptr<Type> type);
  void WriteCallingConvention(CorCallingConvention calling_convention);
  void WriteToken(mdToken token);
  void WriteUInt(ULONG value);

  std::shared_ptr<PCCOR_SIGNATURE> Save();
};

}  // namespace trace::signature

#endif  // _SIGNATURE_PARSER_H_