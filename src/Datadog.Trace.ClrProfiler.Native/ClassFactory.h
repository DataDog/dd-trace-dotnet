#ifndef DD_CLR_PROFILER_CLASS_FACTORY_H_
#define DD_CLR_PROFILER_CLASS_FACTORY_H_

#include <atomic>
#include "unknwn.h"

class ClassFactory : public IClassFactory {
 private:
  std::atomic<int> refCount;

 public:
  ClassFactory();
  virtual ~ClassFactory();
  HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid,
                                           void** ppvObject) override;
  ULONG STDMETHODCALLTYPE AddRef(void) override;
  ULONG STDMETHODCALLTYPE Release(void) override;
  HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* pUnkOuter, REFIID riid,
                                           void** ppvObject) override;
  HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) override;
};

#endif  // DD_CLR_PROFILER_CLASS_FACTORY_H_
