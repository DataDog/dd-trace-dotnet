#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_TOKENS_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_TOKENS_H_

#include "calltarget_tokens.h"

#include <corhlpr.h>

#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "../../../shared/src/native-src/com_ptr.h"
#include "../../../shared/src/native-src/string.h" // NOLINT
#include "clr_helpers.h"
#include "il_rewriter.h"
#include "integration.h"

using namespace shared;
using namespace trace;

namespace fault_tolerant
{

/**
 * FAULT TOLERANT CALLTARGET CONSTANTS
 **/

static const WSTRING managed_profiler_should_heal_name = WStr("ShouldHeal");
static const WSTRING managed_profiler_report_successful_instrumentation = WStr("ReportSuccessfulInstrumentation");
static const WSTRING managed_profiler_fault_tolerant_invoker_type = WStr("Datadog.Trace.FaultTolerant.FaultTolerantInvoker");
static const WSTRING not_implemented;

class FaultTolerantTokens : public CallTargetTokens
{
private:
    // Fault Tolerant members:
    mdMemberRef shouldSelfHealRef = mdMemberRefNil;
    mdMemberRef reportSuccessfulInstrumentationRef = mdMemberRefNil;
    mdTypeRef faultTolerantTypeRef = mdTypeRefNil;
    
protected:
    HRESULT EnsureBaseCalltargetTokens() override;

    const WSTRING& GetCallTargetType() override;
    const WSTRING& GetCallTargetStateType() override;
    const WSTRING& GetCallTargetReturnType() override;
    const WSTRING& GetCallTargetReturnGenericType() override;
    const WSTRING& GetCallTargetRefStructType() override;

public:
    FaultTolerantTokens(ModuleMetadata* module_metadata_ptr);
    
    HRESULT WriteShouldHeal(void* rewriterWrapperPtr, ILInstr** instruction);
    HRESULT WriteReportSuccessfulInstrumentation(void* rewriterWrapperPtr, ILInstr** instruction);
};

} // namespace fault_tolerant

#endif