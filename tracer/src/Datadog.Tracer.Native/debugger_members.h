#ifndef DD_CLR_PROFILER_LIVE_DEBUGGER_H_
#define DD_CLR_PROFILER_LIVE_DEBUGGER_H_

#include "corhlpr.h"
#include "rejit_handler.h"
#include "../../../shared/src/native-src/string.h"
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
    WCHAR* probeId;
    WCHAR* targetType;
    WCHAR* targetMethod;
    WCHAR** targetParameterTypes;
    USHORT targetParameterTypesLength;
} DebuggerMethodProbeDefinition;

typedef struct _DebuggerLineProbeDefinition
{
    WCHAR* probeId;
    GUID mvid;
    mdMethodDef methodId;
    int bytecodeOffset;
    int lineNumber;
    WCHAR* probeFilePath;
    
} DebuggerLineProbeDefinition;

typedef struct _DebuggerRemoveProbesDefinition
{
    WCHAR* probeId;
} DebuggerRemoveProbesDefinition;

struct ProbeDefinition
{
    shared::WSTRING probeId;

    ProbeDefinition(shared::WSTRING&& probeId)
        : probeId(std::move(probeId))
    {
    }

    inline bool operator==(const ProbeDefinition& other) const
    {
        return probeId == other.probeId;
    }

    virtual ~ProbeDefinition() = default;
};

typedef std::shared_ptr<ProbeDefinition> ProbeDefinition_S;

struct MethodProbeDefinition : public ProbeDefinition
{
    const trace::MethodReference target_method;
    const bool is_exact_signature_match = true;

    MethodProbeDefinition(shared::WSTRING probeId, trace::MethodReference&& targetMethod, bool is_exact_signature_match) :
        ProbeDefinition(std::move(probeId)),
        target_method(targetMethod),
        is_exact_signature_match(is_exact_signature_match)
    {
    }

    MethodProbeDefinition(const MethodProbeDefinition& other) :
        ProbeDefinition(other), 
        target_method(other.target_method), 
        is_exact_signature_match(other.is_exact_signature_match)
    {}

    inline bool operator==(const MethodProbeDefinition& other) const
    {
        return probeId == other.probeId && target_method == other.target_method && is_exact_signature_match == other.is_exact_signature_match;
    }
};

typedef std::vector<std::shared_ptr<MethodProbeDefinition>> MethodProbeDefinitions;

struct LineProbeDefinition : public ProbeDefinition
{
    int bytecodeOffset;
    int lineNumber;
    GUID mvid;
    mdMethodDef methodId;
    shared::WSTRING probeFilePath;

    LineProbeDefinition(shared::WSTRING probeId, int bytecodeOffset, int lineNumber, GUID mvid, mdMethodDef methodId,
                        shared::WSTRING probeFilePath) :
        ProbeDefinition(std::move(probeId)), bytecodeOffset(bytecodeOffset), lineNumber(lineNumber), mvid(mvid), methodId(methodId),
        probeFilePath(std::move(probeFilePath))
    {
    }

    LineProbeDefinition(const LineProbeDefinition& other) :
        ProbeDefinition(other), bytecodeOffset(other.bytecodeOffset), lineNumber(other.lineNumber), mvid(other.mvid), methodId(other.methodId),
        probeFilePath(other.probeFilePath)
    {
    }

    inline bool operator==(const LineProbeDefinition& other) const
    {
        return probeId == other.probeId && 
            bytecodeOffset == other.bytecodeOffset && 
            lineNumber == other.lineNumber && 
            mvid == other.mvid && 
            methodId == other.methodId &&
            probeFilePath == other.probeFilePath;
    }
};

typedef std::vector<std::shared_ptr<LineProbeDefinition>> LineProbeDefinitions;

enum class ProbeStatus
{
    RECEIVED,
    INSTALLED,
    BLOCKED,
    /**
     * \brief Preceding with underscore because ERROR is a widely used preprocessor constant.
     */
    // ReSharper disable once CppInconsistentNaming
    _ERROR  // NOLINT(clang-diagnostic-reserved-identifier, bugprone-reserved-identifier)
};

struct ProbeMetadata
{
    shared::WSTRING probeId;
    std::set<trace::MethodIdentifier> methods;
    ProbeStatus status;

    ProbeMetadata() = default;
    ProbeMetadata(const ProbeMetadata& other) = default;
    ProbeMetadata(ProbeMetadata&& other) = default;

    ProbeMetadata(shared::WSTRING probeId, std::set<trace::MethodIdentifier>&& methods, ProbeStatus initialStatus) : probeId(probeId), methods(std::move(methods)), status(initialStatus)
    {
    }
    

    inline bool operator==(const ProbeMetadata& other) const
    {
        return probeId == other.probeId;
    }

    virtual ~ProbeMetadata() = default;
};

typedef struct _ProbeStatusesRequest
{
    WCHAR** probeIds;
    int probeIdsLength;
} ProbeStatusesRequest;

typedef struct _DebuggerProbeStatus
{
    const WCHAR* probeId;
    ProbeStatus status;
} DebuggerProbeStatus;

} // namespace debugger

#endif // DD_CLR_PROFILER_LIVE_DEBUGGER_H_