#ifndef DD_CLR_PROFILER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_METHOD_REWRITER_H_

#include "../../../shared/src/native-src/util.h"
#include "cor.h"
#include "instrumenting_product.h"
#include "module_metadata.h"

struct ILInstr;
class ILRewriterWrapper;

namespace trace
{
    // forward declarations
    class RejitHandlerModule;
    class RejitHandlerModuleMethod;
    class CorProfiler;

class MethodRewriter
{
protected:
    CorProfiler* m_corProfiler;

public:
    MethodRewriter(CorProfiler* corProfiler)
        : m_corProfiler(corProfiler)
    {        
    }

    virtual HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl) = 0;
    virtual InstrumentingProducts GetInstrumentingProduct(RejitHandlerModule* moduleHandler,
                                              RejitHandlerModuleMethod* methodHandler) = 0;
    virtual WSTRING GetInstrumentationId(RejitHandlerModule* moduleHandler,
                                              RejitHandlerModuleMethod* methodHandler) = 0;

    virtual ~MethodRewriter() = default;
};


class TracerMethodRewriter : public MethodRewriter
{
private:
    ILInstr* CreateFilterForException(ILRewriterWrapper* rewriter, mdTypeRef exception, mdTypeRef type_ref, mdMethodDef containsCallTargetBubbleUpException, ULONG exceptionValueIndex);

public:

    TracerMethodRewriter(CorProfiler* corProfiler) : MethodRewriter(corProfiler)
    {
    }

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl) override;

    InstrumentingProducts GetInstrumentingProduct(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
    WSTRING GetInstrumentationId(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;

    std::tuple<WSTRING, WSTRING> GetResourceNameAndOperationName(const ComPtr<IMetaDataImport2>& metadataImport,
                                                             const FunctionInfo* caller,
                                                             TracerTokens* tracerTokens) const;
};

} // namespace trace

#endif // DD_CLR_PROFILER_METHOD_REWRITER_H_