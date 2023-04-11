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

    inline void OnTransform(std::shared_ptr<Sample>& sample, uint32_t valueOffset) const override
    {
        sample->AddValue(1, valueOffset);
        sample->AddLabel(Label(Sample::ExceptionMessageLabel, ExceptionMessage));
        sample->AddLabel(Label(Sample::ExceptionTypeLabel, ExceptionType));
    }

    std::string ExceptionMessage;
    std::string ExceptionType;
};
