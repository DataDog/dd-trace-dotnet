// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.

#include "RawSampleTransformer.h"

#include "OpSysTools.h"
#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "IRuntimeIdStore.h"

#ifdef LINUX
#include "LibrariesInfoCache.h"
#endif

#include <chrono>
#include <string>
#include <string_view>
#include <utility>

std::shared_ptr<Sample> RawSampleTransformer::Transform(const RawSample& rawSample, std::vector<SampleValueTypeProvider::Offset> const& offsets)
{
    auto sample = std::make_shared<Sample>(rawSample.Timestamp, std::string_view(), rawSample.Stack.Size());
    Transform(rawSample, sample, offsets);
    return sample;
}

void RawSampleTransformer::Transform(const RawSample& rawSample, std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& offsets)
{
    sample->Reset();

    auto runtimeId = _pRuntimeIdStore->GetId(rawSample.AppDomainId);

    sample->SetRuntimeId(runtimeId == nullptr ? std::string_view() : std::string_view(runtimeId));
    sample->SetTimestamp(rawSample.Timestamp);

    if (rawSample.LocalRootSpanId != 0 && rawSample.SpanId != 0)
    {
        sample->AddLabel(NumericLabel{Sample::LocalRootSpanIdLabel, rawSample.LocalRootSpanId});
        sample->AddLabel(NumericLabel{Sample::SpanIdLabel, rawSample.SpanId});
    }

    // compute thread/appdomain details
    SetAppDomainDetails(rawSample, sample);
    SetThreadDetails(rawSample, sample);

    // compute symbols for frames
    SetStack(rawSample, sample);

    // allow inherited classes to add values and specific labels
    rawSample.OnTransform(sample, offsets);
}

void RawSampleTransformer::SetAppDomainDetails(const RawSample& rawSample, std::shared_ptr<Sample>& sample)
{
    ProcessID pid;
    std::string appDomainName;

    // check for null AppDomainId (garbage collection for example)
    if (rawSample.AppDomainId == 0)
    {
        sample->SetAppDomainName("CLR");
        sample->SetPid(OpSysTools::GetProcId());

        return;
    }

    if (!_pAppDomainStore->GetInfo(rawSample.AppDomainId, pid, appDomainName))
    {
        sample->SetAppDomainName("");
        sample->SetPid(OpSysTools::GetProcId());

        return;
    }

    sample->SetAppDomainName(std::move(appDomainName));
    sample->SetPid(pid);
}

void RawSampleTransformer::SetThreadDetails(const RawSample& rawSample, std::shared_ptr<Sample>& sample)
{
    // needed for tests
    if (rawSample.ThreadInfo == nullptr)
    {
        // find a way to skip thread details like for garbage collection where no managed threads are involved
        // --> if everything is empty

        if (
            (rawSample.LocalRootSpanId == 0) &&
            (rawSample.SpanId == 0) &&
            (rawSample.AppDomainId == 0) &&
            (rawSample.Stack.Size() == 0))
        {
            sample->SetThreadId("GC");
            sample->SetThreadName("CLR thread (garbage collector)");
            return;
        }

        sample->SetThreadId("<0> [#0]");
        sample->SetThreadName("Managed thread (name unknown) [#0]");

        return;
    }

    sample->SetThreadId(rawSample.ThreadInfo->GetProfileThreadId());
    sample->SetThreadName(rawSample.ThreadInfo->GetProfileThreadName());
}

void RawSampleTransformer::SetStack(const RawSample& rawSample, std::shared_ptr<Sample>& sample)
{
    // Deal with fake stack frames like for garbage collections since the Stack will be empty
    for (auto const& instructionPointer : rawSample.Stack)
    {
        auto [isResolved, frame] = _pFrameStore->GetFrame(instructionPointer);

        if (isResolved)
        {
            sample->AddFrame(frame);
        }
    }
}