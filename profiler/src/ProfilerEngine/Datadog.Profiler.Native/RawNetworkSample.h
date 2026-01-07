// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>

#include "RawSample.h"
#include "Sample.h"
#include "SymbolsStore.h"

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
        EndThreadName(std::move(other.EndThreadName)),
        RedirectUrl(std::move(other.RedirectUrl)),
        HasBeenRedirected(other.HasBeenRedirected),
        DnsWait(other.DnsWait),
        DnsDuration(other.DnsDuration),
        DnsSuccess(other.DnsSuccess),
        HandshakeWait(other.HandshakeWait),
        HandshakeDuration(other.HandshakeDuration),
        HandshakeError(std::move(other.HandshakeError)),
        SocketConnectDuration(other.SocketConnectDuration),
        RequestDuration(other.RequestDuration),
        ResponseDuration(other.ResponseDuration)
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
            EndThreadName = std::move(other.EndThreadName);
            RedirectUrl = std::move(other.RedirectUrl);
            HasBeenRedirected = other.HasBeenRedirected;
            DnsWait = other.DnsWait;
            DnsDuration = other.DnsDuration;
            DnsSuccess = other.DnsSuccess;
            HandshakeWait = other.HandshakeWait;
            HandshakeDuration = other.HandshakeDuration;
            HandshakeError = std::move(other.HandshakeError);
            SocketConnectDuration = other.SocketConnectDuration;
            RequestDuration = other.RequestDuration;
            ResponseDuration = other.ResponseDuration;
        }
        return *this;
    }

    inline void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets, libdatadog::SymbolsStore* symbolsStore) const override
    {
        auto networkCountIndex = valueOffsets[0];
        sample->AddValue((Timestamp - StartTimestamp).count(), networkCountIndex);
        // Note: we don't need to add the start timestamp as a label because it is computed
        // by the backend from the end timestamp and the duration; i.e. the value of this sample

        sample->AddLabel(StringLabel(symbolsStore->GetRequestUrl(), Url));
        sample->AddLabel(NumericLabel(symbolsStore->GetRequestStatusCode(), StatusCode));
        if (!Error.empty())
        {
            sample->AddLabel(StringLabel(symbolsStore->GetRequestError(), Error));
        }
        if (HasBeenRedirected)
        {
            sample->AddLabel(StringLabel(symbolsStore->GetRequestRedirectUrl(), RedirectUrl));
        }
        if (DnsDuration != std::chrono::nanoseconds::zero())
        {
            sample->AddLabel(NumericLabel(symbolsStore->GetRequestDnsWait(), DnsWait.count()));
            sample->AddLabel(NumericLabel(symbolsStore->GetRequestDnsDuration(), DnsDuration.count()));
            sample->AddLabel(StringLabel(symbolsStore->GetRequestDnsSuccess(), DnsSuccess ? "true" : "false"));
        }
        if (HandshakeDuration != std::chrono::nanoseconds::zero())
        {
            sample->AddLabel(NumericLabel(symbolsStore->GetRequestHandshakeWait(), HandshakeWait.count()));
            sample->AddLabel(NumericLabel(symbolsStore->GetRequestHandshakeDuration(), HandshakeDuration.count()));
        }
        if (!HandshakeError.empty())
        {
            sample->AddLabel(StringLabel(symbolsStore->GetRequestHandshakeError(), HandshakeError));
        }
        if (SocketConnectDuration != std::chrono::nanoseconds::zero())
        {
            sample->AddLabel(NumericLabel(symbolsStore->GetRequestSocketDuration(), SocketConnectDuration.count()));
        }
        if (RequestDuration != std::chrono::nanoseconds::zero())  // could be 0 in case of connection/handshake error
        {
            sample->AddLabel(NumericLabel(symbolsStore->GetRequestDuration(), RequestDuration.count()));
        }
        if (ResponseDuration != std::chrono::nanoseconds::zero())  // could be 0 in case of error
        {
            sample->AddLabel(NumericLabel(symbolsStore->GetResponseContentDuration(), ResponseDuration.count()));
        }
        sample->AddLabel(StringLabel(symbolsStore->GetRequestResponseThreadId(), EndThreadId));
        sample->AddLabel(StringLabel(symbolsStore->GetRequestResponseThreadName(), EndThreadName));
    }

    std::string Url;
    std::chrono::nanoseconds StartTimestamp;
    int32_t StatusCode;
    std::string Error;
    std::string EndThreadId;
    std::string EndThreadName;
    std::string RedirectUrl;
    bool HasBeenRedirected;

    std::chrono::nanoseconds DnsWait;
    std::chrono::nanoseconds DnsDuration;
    bool DnsSuccess;

    std::chrono::nanoseconds SocketConnectDuration;

    std::chrono::nanoseconds HandshakeWait;
    std::chrono::nanoseconds HandshakeDuration;
    std::string HandshakeError;

    std::chrono::nanoseconds RequestDuration;
    std::chrono::nanoseconds ResponseDuration;
};