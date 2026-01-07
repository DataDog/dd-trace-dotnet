// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RawThreadLifetimeSample.h"
#include "SymbolsStore.h"

void RawThreadLifetimeSample::OnTransform(
    std::shared_ptr<Sample>& sample,
    std::vector<SampleValueTypeProvider::Offset> const& valueOffset,
    libdatadog::SymbolsStore* symbolsStore) const
{
    // There is no value for threads lifetime events
    // but it could be interesting to compute the life duration of a thread to detect too short lived threads

    if (Kind == ThreadEventKind::Start)
    {
        sample->AddFrame({symbolsStore->GetClrModuleId(), symbolsStore->GetThreadStartFrame(), 0});
        sample->AddLabel(StringLabel(symbolsStore->GetTimelineEventType(), Sample::TimelineEventTypeThreadStart));
    }
    else if (Kind == ThreadEventKind::Stop)
    {
        sample->AddFrame({symbolsStore->GetClrModuleId(), symbolsStore->GetThreadStopFrame(), 0});
        sample->AddLabel(StringLabel(symbolsStore->GetTimelineEventType(), Sample::TimelineEventTypeThreadStop));
    }

    // Set an arbitratry value to avoid being discarded by the backend
    sample->AddValue(1, valueOffset[0]);
}
