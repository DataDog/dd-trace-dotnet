#pragma once
#include "RawSample.h"
#include "Sample.h"

class RawExceptionSample : public RawSample
{
public:
    inline void OnTransform(Sample& sample) const override
    {
        sample.AddValue(1, SampleValue::ExceptionCount);
        sample.AddLabel(Label(Sample::ExceptionMessageLabel, ExceptionMessage));
        sample.AddLabel(Label(Sample::ExceptionTypeLabel, ExceptionType));
    }

    std::string ExceptionMessage;
    std::string ExceptionType;
};
