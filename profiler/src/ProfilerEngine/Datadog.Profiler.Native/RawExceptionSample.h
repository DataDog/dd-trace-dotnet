#pragma once
#include "RawSample.h"
#include "Sample.h"

class RawExceptionSample : public RawSample
{
public:
    RawExceptionSample() = default;

    RawExceptionSample(RawExceptionSample&& other) noexcept
        :
        RawSample(std::move(other)),
        ExceptionMessage(std::move(other.ExceptionMessage)),
        ExceptionType(std::move(other.ExceptionType))
    {
    }

    RawExceptionSample& operator=(RawExceptionSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            ExceptionMessage = std::move(other.ExceptionMessage);
            ExceptionType = std::move(other.ExceptionType);
        }
        return *this;
    }

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets, libdatadog::SymbolsStore* symbolsStore) const override
    {
        assert(valueOffsets.size() == 1);
        sample->AddValue(1, valueOffsets[0]);
        sample->AddLabel(StringLabel(symbolsStore->GetExceptionMessage(), ExceptionMessage));
        sample->AddLabel(StringLabel(symbolsStore->GetExceptionType(), ExceptionType));
    }

    std::string ExceptionMessage;
    std::string ExceptionType;
};