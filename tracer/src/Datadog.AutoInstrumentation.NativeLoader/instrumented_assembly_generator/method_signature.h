#pragma once
#include <memory>
#include <vector>
#include "member_signature.h"

namespace instrumented_assembly_generator
{
class MethodSignature
{
private:
    PCCOR_SIGNATURE _methodSig;
    unsigned _sigLength;
    ULONG _numberOfTypeArguments = 0;
    ULONG _numberOfArguments = 0;
    std::shared_ptr<MemberSignature> _pRet{};
    std::vector<MemberSignature> _arguments;
    bool _isParsed;

public:
    MethodSignature() : _methodSig(nullptr), _sigLength(0), _isParsed(false)
    {
    }

    MethodSignature(PCCOR_SIGNATURE methodSig, unsigned cbBuffer) :
        _methodSig(methodSig), _sigLength(cbBuffer), _isParsed(false)
    {
    }

    [[nodiscard]] ULONG NumberOfTypeArguments() const
    {
        return _numberOfTypeArguments;
    }

    [[nodiscard]] ULONG NumberOfArguments() const
    {
        return _numberOfArguments;
    }

    [[nodiscard]] bool IsValid()
    {
        if (_isParsed)
        {
            return true;
        }
        if (_methodSig == nullptr)
        {
            return false;
        }
        const auto result = Parse();
        return SUCCEEDED(result);
    }

    [[nodiscard]] bool HasThis()
    {
        if (!IsValid())
        {
            return false;
        }
        return CallingConvention() & CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS;
    }

    [[nodiscard]] shared::WSTRING ReturnTypeName(const ComPtr<IMetaDataImport>& metadataImport)
    {
        if (!IsValid() || _pRet == nullptr)
        {
            return WStr("");
        }
        return _pRet->TypeSigToString(metadataImport);
    }

    [[nodiscard]] shared::WSTRING ArgumentsNames(const ComPtr<IMetaDataImport>& metadataImport)
    {
        if (!IsValid() || _numberOfArguments == 0)
        {
            return shared::EmptyWStr;
        }

        shared::WSTRING argumentsNames;
        for (unsigned int i = 0; i < _arguments.size(); ++i)
        {
            argumentsNames += _arguments[i].TypeSigToString(metadataImport);
            if (i + 1 < _arguments.size()) argumentsNames += WStr(",");
        }
        return argumentsNames;
    }

    [[nodiscard]] shared::WSTRING TypeArgumentsNames()
    {
        if (!IsValid() || _numberOfTypeArguments == 0)
        {
            return shared::EmptyWStr;
        }

        shared::WSTRING typeArgumentsNames;
        for (unsigned int i = 0; i < _numberOfTypeArguments; ++i)
        {
            typeArgumentsNames += WStr("T") + shared::ToWSTRING(i);
            if (i + 1 < _numberOfTypeArguments) typeArgumentsNames += WStr(",");
        }
        return typeArgumentsNames;
    }

    [[nodiscard]] CorCallingConvention CallingConvention()
    {
        if (!IsValid())
        {
            return static_cast<CorCallingConvention>(0);
        }
        return static_cast<CorCallingConvention>(_sigLength == 0 ? 0 : _methodSig[0]);
    }

    bool operator==(const MethodSignature& other) const
    {
        return memcmp(_methodSig, other._methodSig, _sigLength);
    }

    HRESULT Parse();
};

}
