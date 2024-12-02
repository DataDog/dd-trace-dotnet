// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkRequestInfo.h"

NetworkRequestInfo::NetworkRequestInfo(std::string url, std::chrono::nanoseconds timestamp)
    :
    NetworkRequestCommon(std::move(url), timestamp)
{
    AppDomainId = 0;
    LocalRootSpanID = 0;
    SpanID = 0;
    DnsResolutionSuccess = false;
    Redirect = nullptr;
}

NetworkRequestInfo::NetworkRequestInfo(NetworkRequestInfo&& other) noexcept
    :
    NetworkRequestCommon(std::move(other))
{
    *this = std::move(other);
}

NetworkRequestInfo& NetworkRequestInfo::operator=(NetworkRequestInfo&& other) noexcept
{
    if (this != &other)
    {
        NetworkRequestCommon::operator=(std::move(other));
        AppDomainId = other.AppDomainId;
        LocalRootSpanID = other.LocalRootSpanID;
        SpanID = other.SpanID;
        DnsResolutionSuccess = other.DnsResolutionSuccess;
        HandshakeError = std::move(other.HandshakeError);
        Error = std::move(other.Error);
        Redirect = std::move(other.Redirect);
    }
    return *this;
}


// --------------------------------------------------------------------------------


NetworkRequestCommon::NetworkRequestCommon(std::string url, std::chrono::nanoseconds timestamp)
    :
    Url(std::move(url)),
    StartTimestamp(timestamp)
{
    DnsWait = std::chrono::nanoseconds::zero();
    DnsStartTime = std::chrono::nanoseconds::zero();
    DnsDuration = std::chrono::nanoseconds::zero();
    SocketConnectStartTime = std::chrono::nanoseconds::zero();
    SocketDuration = std::chrono::nanoseconds::zero();
    HandshakeWait = std::chrono::nanoseconds::zero();
    HandshakeStartTime = std::chrono::nanoseconds::zero();
    HandshakeDuration = std::chrono::nanoseconds::zero();
    ReqRespStartTime = std::chrono::nanoseconds::zero();
    ReqRespDuration = std::chrono::nanoseconds::zero();
}

NetworkRequestCommon::NetworkRequestCommon(NetworkRequestCommon&& other) noexcept
{
    *this = std::move(other);
}

NetworkRequestCommon& NetworkRequestCommon::operator=(NetworkRequestCommon&& other) noexcept
{
    if (this != &other)
    {
        Url = std::move(other.Url);
        StartTimestamp = other.StartTimestamp;

        DnsWait = other.DnsWait;
        DnsStartTime = other.DnsStartTime;
        DnsDuration = other.DnsDuration;
        SocketConnectStartTime = other.SocketConnectStartTime;
        SocketDuration = other.SocketDuration;
        HandshakeWait = other.HandshakeWait;
        HandshakeStartTime = other.HandshakeStartTime;
        HandshakeDuration = other.HandshakeDuration;
        ReqRespStartTime = other.ReqRespStartTime;
        ReqRespDuration = other.ReqRespDuration;
    }
    return *this;
}
