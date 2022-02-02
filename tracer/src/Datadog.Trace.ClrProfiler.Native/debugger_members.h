#ifndef DD_CLR_PROFILER_LIVE_DEBUGGER_H_
#define DD_CLR_PROFILER_LIVE_DEBUGGER_H_

#include "corhlpr.h"
#include "rejit_handler.h"
#include "string.h"
#include "util.h"
#include <corprof.h>

// forward declaration

namespace trace
{
class CorProfiler;
class RejitHandlerModule;
struct MethodReference;
class RejitHandler;
class RejitWorkOffloader;
} // namespace trace

namespace debugger
{
    class DebuggerRejitPreprocessor;

typedef struct _DebuggerMethodProbeDefinition
{
    WCHAR* targetAssembly;
    WCHAR* targetType;
    WCHAR* targetMethod;
    WCHAR** targetParameterTypes;
    USHORT targetParameterTypesLength;
} DebuggerMethodProbeDefinition;

struct MethodProbeDefinition
{
    const trace::MethodReference target_method;

    MethodProbeDefinition()
    {
    }

    MethodProbeDefinition(trace::MethodReference target_method) :
        target_method(target_method)
    {
    }

    inline bool operator==(const MethodProbeDefinition& other) const
    {
        return target_method == other.target_method;
    }
};

} // namespace debugger

#endif // DD_CLR_PROFILER_LIVE_DEBUGGER_H_