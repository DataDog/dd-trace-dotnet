#include "loader.h"

#include "dllmain.h"
#include "logging.h"
#include "resource.h"

#ifdef OSX
#include <mach-o/dyld.h>
#include <mach-o/getsect.h>
#endif

namespace trace {

#ifdef LINUX
extern uint8_t dll_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_start");
extern uint8_t dll_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_end");

extern uint8_t pdb_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_start");
extern uint8_t pdb_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_end");
#endif

Loader* loader = nullptr;

Loader::Loader(ICorProfilerInfo4* info) {
  info_ = info;
  runtime_information_ = GetRuntimeInformation(info);
  loader = this;
}

void Loader::GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray, int* symbolsSize) const {
  Info("Loader::GetAssemblyAndSymbolsBytes");

#ifdef _WIN32
  HINSTANCE hInstance = DllHandle;
  LPCWSTR dllLpName;
  LPCWSTR symbolsLpName;

  if (runtime_information_.is_desktop()) {
    dllLpName = MAKEINTRESOURCE(NET45_MANAGED_ENTRYPOINT_DLL);
    symbolsLpName = MAKEINTRESOURCE(NET45_MANAGED_ENTRYPOINT_SYMBOLS);
  } else {
    dllLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_DLL);
    symbolsLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_SYMBOLS);
  }

  HRSRC hResAssemblyInfo = FindResource(hInstance, dllLpName, L"ASSEMBLY");
  HGLOBAL hResAssembly = LoadResource(hInstance, hResAssemblyInfo);
  *assemblySize = SizeofResource(hInstance, hResAssemblyInfo);
  *pAssemblyArray = (LPBYTE)LockResource(hResAssembly);

  HRSRC hResSymbolsInfo = FindResource(hInstance, symbolsLpName, L"SYMBOLS");
  HGLOBAL hResSymbols = LoadResource(hInstance, hResSymbolsInfo);
  *symbolsSize = SizeofResource(hInstance, hResSymbolsInfo);
  *pSymbolsArray = (LPBYTE)LockResource(hResSymbols);
#elif LINUX
  *assemblySize = dll_end - dll_start;
  *pAssemblyArray = (BYTE*)dll_start;

  *symbolsSize = pdb_end - pdb_start;
  *pSymbolsArray = (BYTE*)pdb_start;
#else
  const auto imgCount = _dyld_image_count();

  for (auto i = 0; i < imgCount; i++) {
    const auto name = std::string(_dyld_get_image_name(i));

    if (name.rfind("Datadog.Trace.ClrProfiler.Native.dylib") !=
        std::string::npos) {
      const auto header =
          (const struct mach_header_64*)_dyld_get_image_header(i);

      unsigned long dllSize;
      const auto dllData = getsectiondata(header, "binary", "dll", &dllSize);
      *assemblySize = dllSize;
      *pAssemblyArray = (BYTE*)dllData;

      unsigned long pdbSize;
      const auto pdbData = getsectiondata(header, "binary", "pdb", &pdbSize);
      *symbolsSize = pdbSize;
      *pSymbolsArray = (BYTE*)pdbData;
      break;
    }
  }
#endif
  return;
}

}