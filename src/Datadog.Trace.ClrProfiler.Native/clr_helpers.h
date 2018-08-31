#ifndef DD_CLR_PROFILER_CLR_HELPERS_H_
#define DD_CLR_PROFILER_CLR_HELPERS_H_

#include <corhlpr.h>
#include <corprof.h>
#include <functional>

#include "ComPtr.h"
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
  Enumerator(
      const std::function<HRESULT(HCORENUM*, T[], ULONG, ULONG*)>& callback,
      const std::function<void(HCORENUM)> close)
      : callback_(callback), close_(close), ptr_(NULL) {}

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
  HRESULT status_;
  T arr_[kEnumeratorMax];
  ULONG idx_;
  ULONG sz_;

 public:
  EnumeratorIterator(const Enumerator<T>* enumerator, HRESULT status)
      : enumerator_(enumerator), status_(status), idx_(0), sz_(0) {}

  bool operator!=(EnumeratorIterator const& other) const {
    return enumerator_ != other.enumerator_ ||
           (status_ == S_OK) != (other.status_ == S_OK);
  }

  T const& operator*() const { return arr_[idx_]; }

  EnumeratorIterator<T>& operator++() {
    if (idx_ < sz_) {
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
    ComPtr<IMetaDataImport> metadata_import) {
  return Enumerator<mdTypeDef>(
      [metadata_import](HCORENUM* ptr, mdTypeDef arr[], ULONG max,
                        ULONG* cnt) -> HRESULT {
        return metadata_import->EnumTypeDefs(ptr, arr, max, cnt);
      },
      [metadata_import](HCORENUM ptr) -> void {
        metadata_import->CloseEnum(ptr);
      });
}

static Enumerator<mdAssemblyRef> EnumAssemblyRefs(
    ComPtr<IMetaDataAssemblyImport> assembly_import) {
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
  AssemblyID id;
  std::wstring name;

  AssemblyInfo() : id(0), name(L"") {}
  AssemblyInfo(AssemblyID id, std::wstring name) : id(id), name(name) {}

  inline bool is_valid() const { return id != 0; }
};

struct ModuleInfo {
  ModuleID id;
  std::wstring path;
  AssemblyInfo assembly;
  DWORD flags;

  ModuleInfo() : id(0), path(L""), assembly({}), flags(0) {}
  ModuleInfo(ModuleID id, std::wstring path, AssemblyInfo assembly, DWORD flags)
      : id(id), path(path), assembly(assembly), flags(flags) {}

  inline bool IsValid() const { return id != 0; }
  inline bool IsWindowsRuntime() const {
    return ((flags & COR_PRF_MODULE_WINDOWS_RUNTIME) != 0);
  }
};

struct TypeInfo {
  mdToken id;
  std::wstring name;

  TypeInfo() : id(0), name(L"") {}
  TypeInfo(mdToken id, std::wstring name) : id(id), name(name) {}

  inline bool IsValid() const { return id != 0; }
};

struct FunctionInfo {
  mdToken id;
  std::wstring name;
  TypeInfo type;

  FunctionInfo() : id(0), name(L""), type({}) {}
  FunctionInfo(mdToken id, std::wstring name, TypeInfo type)
      : id(id), name(name), type(type) {}

  inline bool IsValid() const { return id != 0; }
};

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id);

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import);

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const mdAssemblyRef& assembly_ref);

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport>& metadata_import,
                             const mdToken& function_id);

ModuleInfo GetModuleInfo(ICorProfilerInfo3* info, const ModuleID& module_id);

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport>& metadata_import,
                     const mdToken& type_id);

mdAssemblyRef FindAssemblyRef(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const std::wstring& name);

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

}  // namespace trace

#endif  // DD_CLR_PROFILER_CLR_HELPERS_H_
