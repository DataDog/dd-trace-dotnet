#pragma once

#include "../../../shared/src/native-src/util.h"
#include "cor.h"
#include "instrumenting_product.h"
#include "module_metadata.h"
#include "method_rewriter.h"

struct ILInstr;
class ILRewriterWrapper;

namespace trace
{

class TracerMethodRewriter : public MethodRewriter
{
private:
    ILInstr* CreateFilterForException(ILRewriterWrapper* rewriter, mdTypeRef exception, mdTypeRef type_ref,
                                      mdMethodDef containsCallTargetBubbleUpException, ULONG exceptionValueIndex);

public:
    TracerMethodRewriter(CorProfiler* corProfiler) : MethodRewriter(corProfiler)
    {
    }

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* corProfilerInfo) override;

    InstrumentingProducts GetInstrumentingProduct(RejitHandlerModule* moduleHandler,
                                                  RejitHandlerModuleMethod* methodHandler) override;
    WSTRING GetInstrumentationId(RejitHandlerModule* moduleHandler,
                                 RejitHandlerModuleMethod* methodHandler) override;

    std::tuple<WSTRING, WSTRING> GetResourceNameAndOperationName(const ComPtr<IMetaDataImport2>& metadataImport,
                                                                 const FunctionInfo* caller,
                                                                 TracerTokens* tracerTokens) const;
};

} // namespace trace
