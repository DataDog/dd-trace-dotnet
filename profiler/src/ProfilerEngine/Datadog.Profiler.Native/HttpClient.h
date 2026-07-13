// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

// Minimal raw-socket HTTP/1.1 client used to POST profiles to the Datadog
// agent over plain TCP. It intentionally supports only what the profiler
// export path needs:
//   - a single blocking POST per call
//   - a caller-provided, fully built body (multipart is assembled by the caller)
//   - "Connection: close" semantics (one socket per request, no pooling)
//
// TLS, chunked transfer-encoding, redirects, Unix sockets and Windows named
// pipes are explicitly out of scope for this phase.
class HttpClient
{
public:
    struct Response
    {
        bool Succeeded = false;   // true when a response was received and parsed
        int StatusCode = 0;       // HTTP status code (0 when no response)
        std::string Error;        // populated when Succeeded is false
    };

    using Header = std::pair<std::string, std::string>;

    // Send a POST request to http://host:port/path. The body is sent verbatim.
    // The timeout applies to connect/send/recv operations (milliseconds).
    static Response Post(
        std::string const& host,
        int port,
        std::string const& path,
        std::vector<Header> const& headers,
        std::vector<uint8_t> const& body,
        int timeoutMs);
};
