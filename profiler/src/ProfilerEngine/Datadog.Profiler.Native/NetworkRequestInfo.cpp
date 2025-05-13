// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkRequestInfo.h"

#include <chrono>
using namespace std::chrono_literals;


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

NetworkRequestCommon::NetworkRequestCommon()
{

}

NetworkRequestCommon::NetworkRequestCommon(std::string url, std::chrono::nanoseconds timestamp)
    :
    Url(std::move(url)),
    StartTimestamp(timestamp)
{
    DnsWait = 0ns;
    DnsStartTime = 0ns;
    DnsDuration = 0ns;
    SocketConnectStartTime = 0ns;
    SocketDuration = 0ns;
    HandshakeWait = 0ns;
    HandshakeStartTime = 0ns;
    HandshakeDuration = 0ns;
    RequestHeadersStartTimestamp = 0ns;
    RequestDuration = 0ns;
    ResponseContentStartTimestamp = 0ns;
    ResponseDuration = 0ns;
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
        RequestHeadersStartTimestamp = other.RequestHeadersStartTimestamp;
        RequestDuration = other.RequestDuration;
        ResponseContentStartTimestamp = other.ResponseContentStartTimestamp;
        ResponseDuration = other.ResponseDuration;
    }
    return *this;
}
