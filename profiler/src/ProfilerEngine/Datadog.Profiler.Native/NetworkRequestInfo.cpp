// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkRequestInfo.h"

NetworkRequestInfo::NetworkRequestInfo(std::string url, uint64_t startTimestamp)
    :
    Url(std::move(url)),
    StartTimestamp(startTimestamp)
{
    AppDomainId = 0;
    LocalRootSpanID = 0;
    SpanID = 0;
    DnsStartTime = 0;
    DnsDuration = 0;
    DnsResolutionSuccess = false;
    HandshakeStartTime = 0;
    HandshakeDuration = 0;
    SocketConnectStartTime = 0;
    SocketDuration = 0;
}
