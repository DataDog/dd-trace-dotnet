#include "fault_tolerant_tokens.h"

#include "il_rewriter_wrapper.h"
#include "logger.h"

HRESULT fault_tolerant::FaultTolerantTokens::EnsureBaseCalltargetTokens()
{
    std::lock_guard<std::recursive_mutex> guard(metadata_mutex);

    auto hr = CallTargetTokens::EnsureBaseCalltargetTokens();

    IfFailRet(hr);

    ModuleMetadata* module_metadata = GetMetadata();

    if (faultTolerantTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_fault_tolerant_invoker_type.data(), &faultTolerantTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper faultTolerantTypeRef could not be defined.");
            return hr;
        }
    }

    return S_OK;
}

const WSTRING& fault_tolerant::FaultTolerantTokens::GetCallTargetType()
{
    return not_implemented;
}

const WSTRING& fault_tolerant::FaultTolerantTokens::GetCallTargetStateType()
{
    return not_implemented;
}

const WSTRING& fault_tolerant::FaultTolerantTokens::GetCallTargetReturnType()
{
    return not_implemented;
}

const WSTRING& fault_tolerant::FaultTolerantTokens::GetCallTargetReturnGenericType()
{
    return not_implemented;
}

const WSTRING& fault_tolerant::FaultTolerantTokens::GetCallTargetRefStructType()
{
    return not_implemented;
}

fault_tolerant::FaultTolerantTokens::FaultTolerantTokens(ModuleMetadata* module_metadata_ptr) :
    CallTargetTokens(module_metadata_ptr, true, true)
{
}

HRESULT fault_tolerant::FaultTolerantTokens::WriteShouldHeal(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;

    if (shouldSelfHealRef == mdMemberRefNil)
    {
        ModuleMetadata* module_metadata = GetMetadata();

        unsigned exTypeRefBuffer;
        auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        auto signatureLength = 8 + exTypeRefSize;

        COR_SIGNATURE signature[BUFFER_SIZE];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x05; // (Exception, IntPtr, int, String, int)

        signature[offset++] = ELEMENT_TYPE_BOOLEAN;
        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;
        signature[offset++] = ELEMENT_TYPE_I; // ModuleID (IntPtr)
        signature[offset++] = ELEMENT_TYPE_I4;
        signature[offset++] = ELEMENT_TYPE_STRING;
        signature[offset++] = ELEMENT_TYPE_I4;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(faultTolerantTypeRef,
                                                                  managed_profiler_should_heal_name.data(), signature,
                                                                  signatureLength, &shouldSelfHealRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper shouldSelfHealRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(shouldSelfHealRef, false);
    return S_OK;
}

HRESULT fault_tolerant::FaultTolerantTokens::WriteReportSuccessfulInstrumentation(void* rewriterWrapperPtr,
    ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;

    if (reportSuccessfulInstrumentationRef == mdMemberRefNil)
    {
        ModuleMetadata* module_metadata = GetMetadata();

        auto signatureLength = 7;

        COR_SIGNATURE signature[BUFFER_SIZE];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x04; // (IntPtr, int, String, int)

        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_I; // ModuleID (IntPtr)
        signature[offset++] = ELEMENT_TYPE_I4;
        signature[offset++] = ELEMENT_TYPE_STRING;
        signature[offset++] = ELEMENT_TYPE_I4;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(faultTolerantTypeRef, managed_profiler_report_successful_instrumentation.data(), signature,
                                                                  signatureLength, &reportSuccessfulInstrumentationRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper reportSuccessfulInstrumentationRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(reportSuccessfulInstrumentationRef, false);
    return S_OK;
}
