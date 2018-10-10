#ifndef DD_CLR_PROFILER_CLR_HELPERS_H_
#define DD_CLR_PROFILER_CLR_HELPERS_H_

#include "stdafx.h"

#include <functional>
#include <utility>

#include "com_ptr.h"
#include "integration.h"

namespace trace {

const size_t kNameMaxSize = 1024;
const ULONG kEnumeratorMax = 256;

template <typename T>
class EnumeratorIterator;

template <typename T>
class Enumerator {
 private:
  const std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)> callback_;
  const std::function<void(HCORENUM)> close_;
  mutable HCORENUM ptr_;

 public:
  Enumerator(std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)> callback,
             std::function<void(HCORENUM)> close)
      : callback_(std::move(callback)),
        close_(std::move(close)),
        ptr_(nullptr) {}

  Enumerator(const Enumerator& other) = default;

  Enumerator(Enumerator&& other) noexcept = default;

  Enumerator& operator=(const Enumerator& other) = default;

  Enumerator& operator=(Enumerator&& other) noexcept = default;

  ~Enumerator() { close_(ptr_); }

  EnumeratorIterator<T> begin() const {
    return EnumeratorIterator<T>(this, S_OK);
  }

  EnumeratorIterator<T> end() const {
    return EnumeratorIterator<T>(this, S_FALSE);
  }

  HRESULT Next(T arr[], ULONG max, ULONG* cnt) const {
    return callback_(&ptr_, arr, max, cnt);
  }
};

template <typename T>
class EnumeratorIterator {
 private:
  const Enumerator<T>* enumerator_;
  HRESULT status_ = S_FALSE;
  T arr_[kEnumeratorMax]{};
  ULONG idx_ = 0;
  ULONG sz_ = 0;

 public:
  EnumeratorIterator(const Enumerator<T>* enumerator, HRESULT status)
      : enumerator_(enumerator) {
    if (status == S_OK) {
      status_ = enumerator_->Next(arr_, kEnumeratorMax, &sz_);
      if (status_ == S_OK && sz_ == 0) {
        status_ = S_FALSE;
      }
    } else {
      status_ = status;
    }
  }

  bool operator!=(EnumeratorIterator const& other) const {
    return enumerator_ != other.enumerator_ ||
           (status_ == S_OK) != (other.status_ == S_OK);
  }

  T const& operator*() const { return arr_[idx_]; }

  EnumeratorIterator<T>& operator++() {
    if (idx_ < sz_ - 1) {
      idx_++;
    } else {
      idx_ = 0;
      status_ = enumerator_->Next(arr_, kEnumeratorMax, &sz_);
      if (status_ == S_OK && sz_ == 0) {
        status_ = S_FALSE;
      }
    }
    return *this;
  }
};

static Enumerator<mdTypeDef> EnumTypeDefs(
    const ComPtr<IMetaDataImport2>& metadata_import) {
  return Enumerator<mdTypeDef>(
      [metadata_import](HCORENUM* ptr, mdTypeDef arr[], ULONG max,
                        ULONG* cnt) -> HRESULT {
        return metadata_import->EnumTypeDefs(ptr, arr, max, cnt);
      },
      [metadata_import](HCORENUM ptr) -> void {
        metadata_import->CloseEnum(ptr);
      });
}

static Enumerator<mdTypeRef> EnumTypeRefs(
    const ComPtr<IMetaDataImport2>& metadata_import) {
  return Enumerator<mdTypeRef>(
      [metadata_import](HCORENUM* ptr, mdTypeRef arr[], ULONG max,
                        ULONG* cnt) -> HRESULT {
        return metadata_import->EnumTypeRefs(ptr, arr, max, cnt);
      },
      [metadata_import](HCORENUM ptr) -> void {
        metadata_import->CloseEnum(ptr);
      });
}

static Enumerator<mdMethodDef> EnumMethods(
    const ComPtr<IMetaDataImport2>& metadata_import,
    const mdToken& parent_token) {
  return Enumerator<mdMethodDef>(
      [metadata_import, parent_token](HCORENUM* ptr, mdMethodDef arr[],
                                      ULONG max, ULONG* cnt) -> HRESULT {
        return metadata_import->EnumMethods(ptr, parent_token, arr, max, cnt);
      },
      [metadata_import](HCORENUM ptr) -> void {
        metadata_import->CloseEnum(ptr);
      });
}

static Enumerator<mdMemberRef> EnumMemberRefs(
    const ComPtr<IMetaDataImport2>& metadata_import,
    const mdToken& parent_token) {
  return Enumerator<mdMemberRef>(
      [metadata_import, parent_token](HCORENUM* ptr, mdMemberRef arr[],
                                      ULONG max, ULONG* cnt) -> HRESULT {
        return metadata_import->EnumMemberRefs(ptr, parent_token, arr, max,
                                               cnt);
      },
      [metadata_import](HCORENUM ptr) -> void {
        metadata_import->CloseEnum(ptr);
      });
}

static Enumerator<mdModuleRef> EnumModuleRefs(
    const ComPtr<IMetaDataImport2>& metadata_import) {
  return Enumerator<mdModuleRef>(
      [metadata_import](HCORENUM* ptr, mdModuleRef arr[], ULONG max,
                        ULONG* cnt) -> HRESULT {
        return metadata_import->EnumModuleRefs(ptr, arr, max, cnt);
      },
      [metadata_import](HCORENUM ptr) -> void {
        metadata_import->CloseEnum(ptr);
      });
}

static Enumerator<mdAssemblyRef> EnumAssemblyRefs(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  return Enumerator<mdAssemblyRef>(
      [assembly_import](HCORENUM* ptr, mdAssemblyRef arr[], ULONG max,
                        ULONG* cnt) -> HRESULT {
        return assembly_import->EnumAssemblyRefs(ptr, arr, max, cnt);
      },
      [assembly_import](HCORENUM ptr) -> void {
        assembly_import->CloseEnum(ptr);
      });
}

struct AssemblyInfo {
  const AssemblyID id;
  const std::wstring name;

  AssemblyInfo() : id(0), name(L"") {}
  AssemblyInfo(AssemblyID id, std::wstring name)
      : id(id), name(std::move(name)) {}

  bool is_valid() const { return id != 0; }
};

struct ModuleInfo {
  const ModuleID id;
  const std::wstring path;
  const AssemblyInfo assembly;
  const DWORD flags;

  ModuleInfo() : id(0), path(L""), assembly({}), flags(0) {}
  ModuleInfo(ModuleID id, std::wstring path, AssemblyInfo assembly, DWORD flags)
      : id(id),
        path(std::move(path)),
        assembly(std::move(assembly)),
        flags(flags) {}

  bool IsValid() const { return id != 0; }

  bool IsWindowsRuntime() const {
    return ((flags & COR_PRF_MODULE_WINDOWS_RUNTIME) != 0);
  }
};

struct TypeInfo {
  const mdToken id;
  const std::wstring name;

  TypeInfo() : id(0), name(L"") {}
  TypeInfo(mdToken id, std::wstring name) : id(id), name(std::move(name)) {}

  bool IsValid() const { return id != 0; }
};

struct FunctionInfo {
  const mdToken id;
  const std::wstring name;
  const TypeInfo type;
  const MethodSignature signature;

  FunctionInfo() : id(0), name(L""), type({}), signature() {}
  FunctionInfo(mdToken id, std::wstring name, TypeInfo type,
               MethodSignature signature)
      : id(id),
        name(std::move(name)),
        type(std::move(type)),
        signature(signature) {}

  bool IsValid() const { return id != 0; }
};

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id);

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import);

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const mdAssemblyRef& assembly_ref);

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                             const mdToken& token);

ModuleInfo GetModuleInfo(ICorProfilerInfo3* info, const ModuleID& module_id);

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                     const mdToken& token);

mdAssemblyRef FindAssemblyRef(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const std::wstring& assembly_name);

// FilterIntegrationsByCaller removes any integrations which have a caller and
// its not set to the module
std::vector<Integration> FilterIntegrationsByCaller(
    const std::vector<Integration>& integrations,
    const std::wstring& assembly_name);

// FilterIntegrationsByTarget removes any integrations which have a target not
// referenced by the module's assembly import
std::vector<Integration> FilterIntegrationsByTarget(
    const std::vector<Integration>& integrations,
    const ComPtr<IMetaDataAssemblyImport>& assembly_import);

mdMethodSpec DefineMethodSpec(const ComPtr<IMetaDataEmit2>& metadata_emit,
                              const mdToken& token,
                              const MethodSignature& signature);

}  // namespace trace

#endif  // DD_CLR_PROFILER_CLR_HELPERS_H_
