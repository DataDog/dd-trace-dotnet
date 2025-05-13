// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <string>

#include "Callstack.h"
#include "ManagedThreadInfo.h"


/*
    A successful request triggers the following sequence of events, corresponding to the different DNS/socket/security phases:
    --------------------------------------------------------------------------------------------------------------------
     78536 | 00000011-0000-0000-0000-0000f26a9d59 =              1/1 > event   1 __ [ 1| Start] RequestStart
           |> https://www.datadoghq.com:443/blog/engineering/dotnet-continuous-profiler-part-4/
     26316 | 00001011-0000-0000-0000-0000f25a9d59 =            1/1/1 > event   1 __ [ 1| Start] ResolutionStart
          R|> www.datadoghq.com
     26316 | 00001011-0000-0000-0000-0000f25a9d59 =            1/1/1 > event   2 __ [ 2|  Stop] ResolutionStop
          <|R
     26316 | 00002011-0000-0000-0000-0000f24a9d59 =            1/1/2 > event   1 __ [ 1| Start] ConnectStart
          S|> InterNetworkV6:28:{1,187,0,0,0,0,38,0,144,0,33,99,164,0,0,0,250,147,128,192,147,161,0,0,0,0}
     26316 | 00002011-0000-0000-0000-0000f24a9d59 =            1/1/2 > event   2 __ [ 2|  Stop] ConnectStop
          <|S
     26316 | 00003011-0000-0000-0000-0000f23a9d59 =            1/1/3 > event   1 __ [ 1| Start] HandshakeStart
        SEC|> www.datadoghq.com - isServer = False
     26316 | 00003011-0000-0000-0000-0000f23a9d59 =            1/1/3 > event   2 __ [ 2|  Stop] HandshakeStop
          <|SEC Tls13
     26316 | 00000011-0000-0000-0000-0000f26a9d59 =              1/1 > event   4 __ [ 0|  Info] ConnectionEstablished
           |= [  0] https://www.datadoghq.com:443/2600:9000:2163:a400:0:fa93:80c0:93a1
     61232 | 00000011-0000-0000-0000-0000f26a9d59 =              1/1 > event   6 __ [ 0|  Info] RequestLeftQueue
           |  wait 603.1275 ms in queue
     61232 | 00004011-0000-0000-0000-0000f22a9d59 =            1/1/4 > event   7 __ [ 1| Start] RequestHeadersStart
           |QH[  0]
     61232 | 00004011-0000-0000-0000-0000f22a9d59 =            1/1/4 > event   8 __ [ 2|  Stop] RequestHeadersStop
          <|QH
     61232 | 00005011-0000-0000-0000-0000f21a9d59 =            1/1/5 > event  11 __ [ 1| Start] ResponseHeadersStart
           |RH>
     61232 | 00005011-0000-0000-0000-0000f21a9d59 =            1/1/5 > event  12 __ [ 2|  Stop] ResponseHeadersStop
      200 <|RH
     61232 | 00006011-0000-0000-0000-0000f20a9d59 =            1/1/6 > event  13 __ [ 1| Start] ResponseContentStart
           |RC>
     26316 | 00006011-0000-0000-0000-0000f20a9d59 =            1/1/6 > event  14 __ [ 2|  Stop] ResponseContentStop
          <|RC
     26316 | 00000011-0000-0000-0000-0000f26a9d59 =              1/1 > event   2 __ [ 2|  Stop] RequestStop
      200 <|
    --------------------------------------------------------------------------------------------------------------------
    - Note that Resolution/Connect/Handshake events may not be present in all requests once the DNS/socket/security are resolved.
    - In case of errors, the RequestFailed event is emitted before RequestStop and, again, some events might not be emitted
      depending on which phase is failing.
    - The duration stored in the RequestLeftQueue payload is not really clear so we will compute the duration/wait of each phase.
    - The wait/queueing time corresponds to the time between the end of a phase and the beginning of the next one.


    In case of redirect, the first phases happen until the RequestHeadersStop event, then the Redirect event is emitted and
    the next phases start again from the ResolutionStart (if needed) event:
    --------------------------------------------------------------------------------------------------------------------
     81044 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event   1 __ [ 1| Start] RequestStart
       |> http://github.com:80/Maoni0
     80236 | 00001011-0000-0000-0000-00003ae29c59 =            1/1/1 > event   1 __ [ 1| Start] ResolutionStart
          R|> github.com
     80236 | 00001011-0000-0000-0000-00003ae29c59 =            1/1/1 > event   2 __ [ 2|  Stop] ResolutionStop
          <|R
     80236 | 00002011-0000-0000-0000-00003af29c59 =            1/1/2 > event   1 __ [ 1| Start] ConnectStart
          S|> InterNetworkV6:28:{0,80,0,0,0,0,0,0,0,0,0,0,0,0,0,0,255,255,140,82,116,4,0,0,0,0}
     80236 | 00002011-0000-0000-0000-00003af29c59 =            1/1/2 > event   2 __ [ 2|  Stop] ConnectStop
          <|S
     80236 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event   4 __ [ 0|  Info] ConnectionEstablished
           |= [  0] http://github.com:80/::ffff:140.82.116.4
     60712 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event   6 __ [ 0|  Info] RequestLeftQueue
           |  wait 358.5052 ms in queue
     60712 | 00003011-0000-0000-0000-00003a829c59 =            1/1/3 > event   7 __ [ 1| Start] RequestHeadersStart
           |QH[  0]
     60712 | 00003011-0000-0000-0000-00003a829c59 =            1/1/3 > event   8 __ [ 2|  Stop] RequestHeadersStop
          <|QH
     60712 | 00004011-0000-0000-0000-00003a929c59 =            1/1/4 > event  11 __ [ 1| Start] ResponseHeadersStart
           |RH>
     60712 | 00004011-0000-0000-0000-00003a929c59 =            1/1/4 > event  12 __ [ 2|  Stop] ResponseHeadersStop
      301 <|RH
     60712 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event  16 __ [ 0|  Info] Redirect
          R|> https://github.com/Maoni0
     80236 | 00005011-0000-0000-0000-00003aa29c59 =            1/1/5 > event   1 __ [ 1| Start] ResolutionStart
          R|> github.com
     80236 | 00005011-0000-0000-0000-00003aa29c59 =            1/1/5 > event   2 __ [ 2|  Stop] ResolutionStop
          <|R
     80236 | 00006011-0000-0000-0000-00003ab29c59 =            1/1/6 > event   1 __ [ 1| Start] ConnectStart
          S|> InterNetworkV6:28:{1,187,0,0,0,0,0,0,0,0,0,0,0,0,0,0,255,255,140,82,116,4,0,0,0,0}
     80236 | 00006011-0000-0000-0000-00003ab29c59 =            1/1/6 > event   2 __ [ 2|  Stop] ConnectStop
          <|S
     80236 | 00007011-0000-0000-0000-00003a429f59 =            1/1/7 > event   1 __ [ 1| Start] HandshakeStart
        SEC|> github.com - isServer = False
     80236 | 00007011-0000-0000-0000-00003a429f59 =            1/1/7 > event   2 __ [ 2|  Stop] HandshakeStop
          <|SEC Tls13
     80236 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event   4 __ [ 0|  Info] ConnectionEstablished
           |= [  1] https://github.com:443/::ffff:140.82.116.4
     60712 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event   6 __ [ 0|  Info] RequestLeftQueue
           |  wait 362.0433 ms in queue
     60712 | 00008011-0000-0000-0000-00003a529f59 =            1/1/8 > event   7 __ [ 1| Start] RequestHeadersStart
           |QH[  1]
     60712 | 00008011-0000-0000-0000-00003a529f59 =            1/1/8 > event   8 __ [ 2|  Stop] RequestHeadersStop
          <|QH
     60712 | 00009011-0000-0000-0000-00003a629f59 =            1/1/9 > event  11 __ [ 1| Start] ResponseHeadersStart
           |RH>
     60712 | 00009011-0000-0000-0000-00003a629f59 =            1/1/9 > event  12 __ [ 2|  Stop] ResponseHeadersStop
      200 <|RH
     60712 | 0000a011-0000-0000-0000-00003a729f59 =           1/1/10 > event  13 __ [ 1| Start] ResponseContentStart
           |RC>
     60712 | 0000a011-0000-0000-0000-00003a729f59 =           1/1/10 > event  14 __ [ 2|  Stop] ResponseContentStop
          <|RC
     60712 | 00000011-0000-0000-0000-00003ad29c59 =              1/1 > event   2 __ [ 2|  Stop] RequestStop
      200 <|
    --------------------------------------------------------------------------------------------------------------------
    In a Redirect case, the durations of the different phases are cumulated across the original and redirected requests.

    Known limitations:
    - .NET 5 and 6 do not emit HTTP events at all
    - .NET 7 does not emit Redirect event

*/

// Used to store phases details shared between a request and a redirected one
// The timestamp is the one of the request Start or Redirect events
// The URL is the one of the request or the redirect
class NetworkRequestCommon
{
public:
    NetworkRequestCommon();
    NetworkRequestCommon(std::string url, std::chrono::nanoseconds timestamp);
    ~NetworkRequestCommon() = default;

    // needed to store it in a map
    NetworkRequestCommon(const NetworkRequestCommon&) = delete;
    NetworkRequestCommon& operator=(const NetworkRequestCommon&) = delete;
    NetworkRequestCommon(NetworkRequestCommon&& other) noexcept;
    NetworkRequestCommon& operator=(NetworkRequestCommon&& other) noexcept;

public:
    // used both for initial request and for redirect that could be empty in .NET 7
    std::string Url;

    // correspond to the timestamp of request Start or Redirect events
    std::chrono::nanoseconds StartTimestamp;

    // DNS
    std::chrono::nanoseconds DnsWait;
    std::chrono::nanoseconds DnsStartTime;
    std::chrono::nanoseconds DnsDuration;

    // HTTPS
    std::chrono::nanoseconds HandshakeWait;
    std::chrono::nanoseconds HandshakeStartTime;
    std::chrono::nanoseconds HandshakeDuration;

    // socket connection
    std::chrono::nanoseconds SocketConnectStartTime;
    std::chrono::nanoseconds SocketDuration;

    // send request header + content and receive response header
    std::chrono::nanoseconds RequestHeadersStartTimestamp;
    std::chrono::nanoseconds RequestDuration;

    // receive response content
    std::chrono::nanoseconds ResponseContentStartTimestamp;
    std::chrono::nanoseconds ResponseDuration;
};

// Used to store the details of a complete request; including a redirected one stored in the Redirect field
class NetworkRequestInfo : public NetworkRequestCommon
{
public:
    NetworkRequestInfo(std::string url, std::chrono::nanoseconds timestamp);
    ~NetworkRequestInfo() = default;

    // needed to store it in a map
    NetworkRequestInfo(const NetworkRequestInfo&) = delete;
    NetworkRequestInfo& operator=(const NetworkRequestInfo&) = delete;
    NetworkRequestInfo(NetworkRequestInfo&& other) noexcept;
    NetworkRequestInfo& operator=(NetworkRequestInfo&& other) noexcept;

public:
    // request start
    uint64_t LocalRootSpanID;
    uint64_t SpanID;
    AppDomainID AppDomainId;
    Callstack StartCallStack;
    std::shared_ptr<ManagedThreadInfo> StartThreadInfo;

    // In case of redirection, the DNS resolution might succeed for the initial url but fails for the redirected one
    bool DnsResolutionSuccess;
    std::string HandshakeError;
    std::string Error;

    // set when redirect event is received or repetition of the request events detected in .NET 7
    std::unique_ptr<NetworkRequestCommon> Redirect;
};