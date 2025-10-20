// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RawThreadLifetimeSample.h"
#include "SymbolsStore.h"

void RawThreadLifetimeSample::OnTransform(
    std::shared_ptr<Sample>& sample,
    std::vector<SampleValueTypeProvider::Offset> const& valueOffset,
    libdatadog::SymbolsStore* symbolsStore) const
{
    
    static const std::string TimelineEventTypeThreadStart = "thread start";
    static const std::string TimelineEventTypeThreadStop = "thread stop";
    // There is no value for threads lifetime events
    // but it could be interesting to compute the life duration of a thread to detect too short lived threads

    // TODO: add fake frames for start and stop
    if (Kind == ThreadEventKind::Start)
    {
        //sample->AddFrame({EmptyModule, StartFrame, "", 0});
        sample->AddLabel(StringLabel(symbolsStore->GetTimelineEventType(), TimelineEventTypeThreadStart));
    }
    else if (Kind == ThreadEventKind::Stop)
    {
        //sample->AddFrame({EmptyModule, StopFrame, "", 0});
        sample->AddLabel(StringLabel(symbolsStore->GetTimelineEventType(), TimelineEventTypeThreadStop));
    }

    // Set an arbitratry value to avoid being discarded by the backend
    sample->AddValue(1, valueOffset[0]);
}
