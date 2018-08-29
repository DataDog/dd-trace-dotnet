#ifndef DD_CLR_PROFILER_ITERATORS_H_
#define DD_CLR_PROFILER_ITERATORS_H_

#include <corhlpr.h>
#include <functional>

#include "ComPtr.h"

namespace trace {

const ULONG ENUMERATOR_MAX = 256;

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
  T arr_[ENUMERATOR_MAX];
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
      status_ = enumerator_->Next(arr_, ENUMERATOR_MAX, &sz_);
      if (status_ == S_OK && sz_ == 0) {
        status_ = S_FALSE;
      }
    }
    return *this;
  }
};

static Enumerator<mdAssemblyRef> EnumAssemblyRefs(
    ComPtr<IMetaDataAssemblyImport> assemblyImport) {
  auto callback = [assemblyImport](HCORENUM* ptr, mdAssemblyRef arr[],
                                   ULONG max, ULONG* cnt) -> HRESULT {
    return assemblyImport->EnumAssemblyRefs(ptr, arr, max, cnt);
  };
  auto close = [assemblyImport](HCORENUM ptr) -> void {
    assemblyImport->CloseEnum(ptr);
  };
  return Enumerator<mdAssemblyRef>(callback, close);
}

}  // namespace trace

#endif  // DD_CLR_PROFILER_ITERATORS_H_