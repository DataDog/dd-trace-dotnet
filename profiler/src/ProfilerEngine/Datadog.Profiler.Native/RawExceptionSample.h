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

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
    {
        assert(valueOffsets.size() == 1);
        sample->AddValue(1, valueOffsets[0]);
        sample->AddLabel(StringLabel(Sample::ExceptionMessageLabel, ExceptionMessage));
        sample->AddLabel(StringLabel(Sample::ExceptionTypeLabel, ExceptionType));

        // for unhandled exceptions, we need to add a fake callstack
        // and a special label
        if (!ThrowingMethod.Frame.empty())
        {
            // each unhandled exception is shown under the CLR root
            sample->AddFrame({ EmptyModule, RootFrame, "", 0 });
            sample->AddFrame({ ThrowingMethod.ModuleName, ThrowingMethod.Frame, ThrowingMethod.Filename, 0 });

            sample->AddLabel(StringLabel(Sample::ExceptionUnhandledLabel, "true"));
        }
    }

    std::string ExceptionMessage;
    std::string ExceptionType;
    FrameInfoView ThrowingMethod;

private:
    // an unhandled exception will be shown under the CLR root
    static constexpr inline std::string_view EmptyModule = "CLR";
    static constexpr inline std::string_view RootFrame = "|lm: |ns: |ct: |cg: |fn:Unhandled exception |fg: |sg:";
};
