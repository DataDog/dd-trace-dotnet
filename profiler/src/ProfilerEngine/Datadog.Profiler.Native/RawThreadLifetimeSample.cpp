// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RawThreadLifetimeSample.h"

void RawThreadLifetimeSample::OnTransform(
    std::shared_ptr<Sample>& sample,
    std::vector<SampleValueTypeProvider::Offset> const& valueOffset,
    libdatadog::SymbolsStore* pSymbolsStore) const
{
    // There is no value for threads lifetime events
    // but it could be interesting to compute the life duration of a thread to detect too short lived threads

    if (Kind == ThreadEventKind::Start)
    {
        sample->AddFrame({pSymbolsStore->GetClrModuleId(), pSymbolsStore->GetThreadStartFrame(), 0});
        sample->AddLabel(StringLabel(pSymbolsStore->GetTimelineEventType(), Sample::TimelineEventTypeThreadStart));
    }
    else if (Kind == ThreadEventKind::Stop)
    {
        sample->AddFrame({pSymbolsStore->GetClrModuleId(), pSymbolsStore->GetThreadStopFrame(), 0});
        sample->AddLabel(StringLabel(pSymbolsStore->GetTimelineEventType(), Sample::TimelineEventTypeThreadStop));
    }

    // Set an arbitratry value to avoid being discarded by the backend
    sample->AddValue(1, valueOffset[0]);
}
