#pragma once
#include "RawSample.h"
#include "Sample.h"

class RawExceptionSample : public RawSample
{
public:
    inline void OnTransform(Sample& sample, uint32_t valueOffset) const override
    {
        sample.AddValue(1, valueOffset);
        sample.AddLabel(Label(Sample::ExceptionMessageLabel, ExceptionMessage));
        sample.AddLabel(Label(Sample::ExceptionTypeLabel, ExceptionType));
    }

    std::string ExceptionMessage;
    std::string ExceptionType;
};
