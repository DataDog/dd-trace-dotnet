#ifndef DD_CLR_PROFILER_DLLMAIN_H_
#define DD_CLR_PROFILER_DLLMAIN_H_

#include "class_factory.h"

#ifdef _WIN32
#define EXTERN extern
#else
#define EXTERN extern __attribute__((visibility("default")))
#endif

EXTERN HINSTANCE DllHandle;

#endif // DD_CLR_PROFILER_DLLMAIN_H_