// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkRequestInfo.h"

NetworkRequestInfo::NetworkRequestInfo(std::string url, std::chrono::nanoseconds startTimestamp)
    :
    Url(std::move(url)),
    StartTimestamp(startTimestamp)
{
    AppDomainId = 0;
    LocalRootSpanID = 0;
    SpanID = 0;
    DnsStartTime = std::chrono::nanoseconds::zero();
    DnsDuration = std::chrono::nanoseconds::zero();
    DnsResolutionSuccess = false;
    HandshakeStartTime = std::chrono::nanoseconds::zero();
    HandshakeDuration = std::chrono::nanoseconds::zero();
    SocketConnectStartTime = std::chrono::nanoseconds::zero();
    SocketDuration = std::chrono::nanoseconds::zero();
    ReqRespStartTime = std::chrono::nanoseconds::zero();
    ReqRespDuration = std::chrono::nanoseconds::zero();
}
