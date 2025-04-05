// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Sample.h"

// define well known label string constants
const InternedString Sample::ThreadIdLabel = "thread id";
const InternedString Sample::ThreadNameLabel = "thread name";
const InternedString Sample::AppDomainNameLabel = "appdomain name";
const InternedString Sample::ProcessIdLabel = "appdomain process id";
const InternedString Sample::LocalRootSpanIdLabel = "local root span id";
const InternedString Sample::SpanIdLabel = "span id";
const std::string Sample::ExceptionTypeLabel = "exception type";
const InternedString Sample::ExceptionMessageLabel = "exception message";
const InternedString Sample::AllocationClassLabel = "allocation class";

// garbage collection related labels
const InternedString Sample::TimelineEventTypeLabel = "event";
    const std::string Sample::TimelineEventTypeThreadStart = "thread start";
    const std::string Sample::TimelineEventTypeThreadStop = "thread stop";
    const std::string Sample::TimelineEventTypeStopTheWorld = "stw";
    const std::string Sample::TimelineEventTypeGarbageCollection = "gc";
        const InternedString Sample::GarbageCollectionReasonLabel = "gc reason";   // look at GCReason enumeration
        const InternedString Sample::GarbageCollectionTypeLabel = "gc type";       // look at GCType enumeration
        const InternedString Sample::GarbageCollectionCompactingLabel = "gc compacting"; // true or false
const InternedString Sample::GarbageCollectionGenerationLabel = "gc generation";
const InternedString Sample::GarbageCollectionNumberLabel = "gc number";

// life object related labels
const InternedString Sample::ObjectLifetimeLabel = "object lifetime";
const InternedString Sample::ObjectIdLabel = "object id";
const InternedString Sample::ObjectGenerationLabel = "object generation";

// network requests related labels
const InternedString Sample::RequestUrlLabel = "request url";
const InternedString Sample::RequestStatusCodeLabel = "response status code";
const InternedString Sample::RequestErrorLabel = "response error";
const InternedString Sample::RequestRedirectUrlLabel = "redirect url";
const InternedString Sample::RequestDnsWaitLabel = "dns.wait";
const InternedString Sample::RequestDnsDurationLabel = "dns.duration";
const InternedString Sample::RequestDnsSuccessLabel = "dns.success";
const InternedString Sample::RequestHandshakeWaitLabel = "tls.wait";
const InternedString Sample::RequestHandshakeDurationLabel = "tls.duration";
const InternedString Sample::RequestHandshakeErrorLabel = "tls.error";
const InternedString Sample::RequestSocketDurationLabel = "socket.duration";
const InternedString Sample::RequestDurationLabel = "request.duration";
const InternedString Sample::ResponseContentDurationLabel = "response_content.duration";
const InternedString Sample::RequestResponseThreadIdLabel = "response.thread_id";
const InternedString Sample::RequestResponseThreadNameLabel = "response.thread_name";

// TODO: update the values vector size if more than 16 slots are needed
size_t Sample::ValuesCount = 16;  // should be set BEFORE any sample gets created


Sample::Sample(std::chrono::nanoseconds timestamp, std::string_view runtimeId, size_t framesCount) :
    Sample(runtimeId)
{
    _timestamp = timestamp;
    _runtimeId = runtimeId;
    _callstack.reserve(framesCount);
    _labels.reserve(10);
    _numericLabels.reserve(10);
}

Sample::Sample(std::string_view runtimeId) :
    _values(ValuesCount),
    _timestamp{0},
    _labels{},
    _numericLabels{},
    _callstack{},
    _runtimeId{runtimeId}
{
}

std::chrono::nanoseconds Sample::GetTimeStamp() const
{
    return _timestamp;
}

const Values& Sample::GetValues() const
{
    return _values;
}

/// <summary>
/// Since this class is not finished, this method is only for test purposes
/// </summary>
/// <param name="value"></param>
void Sample::SetValue(std::int64_t value)
{
    _values[0] = value;
}

void Sample::AddValue(std::int64_t value, size_t index)
{
    if (index >= ValuesCount)
    {
        // TODO: fix compilation error about std::stringstream
        // std::stringstream builder;
        // builder << "\"index\" (=" << index << ") is greater than limit (=" << array_size << ")";
        // throw std::invalid_argument(builder.str());
        throw std::invalid_argument("index");
    }

    _values[index] = value;
}

void Sample::AddFrame(FrameInfoView const& frame)
{
    _callstack.push_back(frame);
}

const std::vector<FrameInfoView>& Sample::GetCallstack() const
{
    return _callstack;
}

std::string_view Sample::GetRuntimeId() const
{
    return _runtimeId;
}

const Labels& Sample::GetLabels() const
{
    return _labels;
}

const NumericLabels& Sample::GetNumericLabels() const
{
    return _numericLabels;
}
