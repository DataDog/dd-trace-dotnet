// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>

#include "RawSample.h"
#include "Sample.h"

class RawNetworkSample : public RawSample
{
public:
    RawNetworkSample() = default;

    RawNetworkSample(RawNetworkSample&& other) noexcept
        :
        RawSample(std::move(other)),
        Url(std::move(other.Url)),
        StartTimestamp(other.StartTimestamp),
        StatusCode(other.StatusCode),
        Error(std::move(other.Error)),
        EndThreadId(std::move(other.EndThreadId)),
        RedirectUrl(std::move(other.RedirectUrl)),
        DnsStartTimestamp(other.DnsStartTimestamp),
        DnsDuration(other.DnsDuration),
        DnsSuccess(other.DnsSuccess),
        HandshakeDuration(other.HandshakeDuration),
        HandshakeError(std::move(other.HandshakeError)),
        SocketConnectDuration(other.SocketConnectDuration),
        ReqRespStartTimestamp(other.ReqRespStartTimestamp),
        ReqRespDuration(other.ReqRespDuration)
    {
    }

    RawNetworkSample& operator=(RawNetworkSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            Url = std::move(other.Url);
            StartTimestamp = other.StartTimestamp;
            StatusCode = other.StatusCode;
            Error = std::move(other.Error);
            EndThreadId = std::move(other.EndThreadId);
            RedirectUrl = std::move(other.RedirectUrl);
            DnsStartTimestamp = other.DnsStartTimestamp;
            DnsDuration = other.DnsDuration;
            DnsSuccess = other.DnsSuccess;
            HandshakeDuration = other.HandshakeDuration;
            HandshakeError = std::move(other.HandshakeError);
            SocketConnectDuration = other.SocketConnectDuration;
            ReqRespStartTimestamp = other.ReqRespStartTimestamp;
            ReqRespDuration = other.ReqRespDuration;
        }
        return *this;
    }

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
    {
        auto networkCountIndex = valueOffsets[0];
        sample->AddValue((Timestamp - StartTimestamp).count(), networkCountIndex);

        sample->AddLabel(Label(Sample::RequestUrlLabel, Url));
        sample->AddNumericLabel(NumericLabel(Sample::RequestTimeStampLabel, StartTimestamp.count()));
        sample->AddNumericLabel(NumericLabel(Sample::RequestStatusCodeLabel, StatusCode));
        if (!Error.empty())
        {
            sample->AddLabel(Label(Sample::RequestErrorLabel, Error));
        }
        if (!RedirectUrl.empty())
        {
            sample->AddLabel(Label(Sample::RequestRedirectUrlLabel, RedirectUrl));
        }
        if (DnsDuration != std::chrono::nanoseconds::zero())
        {
            sample->AddNumericLabel(NumericLabel(Sample::RequestDnsDurationLabel, DnsDuration.count()));
            sample->AddLabel(Label(Sample::RequestDnsSuccessLabel, DnsSuccess ? "true" : "false"));
        }
        if (HandshakeDuration != std::chrono::nanoseconds::zero())
        {
            sample->AddNumericLabel(NumericLabel(Sample::RequestHandshakeDurationLabel, HandshakeDuration.count()));
        }
        if (!HandshakeError.empty())
        {
            sample->AddLabel(Label(Sample::RequestHandshakeErrorLabel, HandshakeError));
        }
        if (SocketConnectDuration != std::chrono::nanoseconds::zero())
        {
            sample->AddNumericLabel(NumericLabel(Sample::RequestSocketDurationLabel, SocketConnectDuration.count()));
        }
        sample->AddLabel(Label(Sample::RequestResponseThreadIdLabel, EndThreadId));
        if (ReqRespDuration != std::chrono::nanoseconds::zero())  // could be 0 in case of error
        {
            sample->AddNumericLabel(NumericLabel(Sample::RequestResponseDurationLabel, ReqRespDuration.count()));
        }
    }

    std::string Url;
    std::chrono::nanoseconds StartTimestamp;
    int32_t StatusCode;
    std::string Error;
    std::string EndThreadId;
    std::string RedirectUrl;

    std::chrono::nanoseconds DnsStartTimestamp;
    std::chrono::nanoseconds DnsDuration;
    bool DnsSuccess;

    std::chrono::nanoseconds HandshakeDuration;
    std::string HandshakeError;

    std::chrono::nanoseconds SocketConnectDuration;

    std::chrono::nanoseconds ReqRespStartTimestamp;
    std::chrono::nanoseconds ReqRespDuration;

    // TODO: check with BE if we also need the thread name
};