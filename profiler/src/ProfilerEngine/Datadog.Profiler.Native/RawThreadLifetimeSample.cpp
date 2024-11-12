// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RawThreadLifetimeSample.h"

void RawThreadLifetimeSample::OnTransform(
    std::shared_ptr<Sample>& sample,
    std::vector<SampleValueTypeProvider::Offset> const& valueOffset) const
{
    // There is no value for threads lifetime events
    // but it could be interesting to compute the life duration of a thread to detect too short lived threads

    if (Kind == ThreadEventKind::Start)
    {
        sample->AddFrame({EmptyModule, StartFrame, "", 0});
        sample->AddLabel(Label(Sample::TimelineEventTypeLabel, Sample::TimelineEventTypeThreadStart));
    }
    else if (Kind == ThreadEventKind::Stop)
    {
        sample->AddFrame({EmptyModule, StopFrame, "", 0});
        sample->AddLabel(Label(Sample::TimelineEventTypeLabel, Sample::TimelineEventTypeThreadStop));
    }

    // Set an arbitratry value to avoid being discarded by the backend
    sample->AddValue(1, valueOffset[0]);
}
