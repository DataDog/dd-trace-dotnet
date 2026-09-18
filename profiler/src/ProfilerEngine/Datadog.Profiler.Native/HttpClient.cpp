// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HttpClient.h"

#include <cerrno>
#include <cstring>
#include <mutex>
#include <sstream>

#ifdef _WINDOWS
#include <winsock2.h>
#include <ws2tcpip.h>
// clang-format off
#include <windows.h>
// clang-format on
using socket_t = SOCKET;
constexpr socket_t InvalidSocket = INVALID_SOCKET;
#else
#include <arpa/inet.h>
#include <netdb.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <sys/types.h>
#include <unistd.h>
using socket_t = int;
constexpr socket_t InvalidSocket = -1;
#endif

namespace {

#ifdef _WINDOWS
void EnsureWinsockInitialized()
{
    static std::once_flag onceFlag;
    std::call_once(onceFlag, []() {
        WSADATA wsaData;
        // Intentionally not cleaned up: Winsock stays initialized for the
        // lifetime of the process (the profiler exports periodically).
        WSAStartup(MAKEWORD(2, 2), &wsaData);
    });
}

void CloseSocket(socket_t s)
{
    ::closesocket(s);
}

std::string LastSocketError()
{
    return "WSA error " + std::to_string(WSAGetLastError());
}
#else
void CloseSocket(socket_t s)
{
    ::close(s);
}

std::string LastSocketError()
{
    return std::string(std::strerror(errno)) + " (" + std::to_string(errno) + ")";
}
#endif

void SetTimeouts(socket_t s, int timeoutMs)
{
#ifdef _WINDOWS
    DWORD timeout = static_cast<DWORD>(timeoutMs);
    setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));
    setsockopt(s, SOL_SOCKET, SO_SNDTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));
#else
    struct timeval tv;
    tv.tv_sec = timeoutMs / 1000;
    tv.tv_usec = (timeoutMs % 1000) * 1000;
    setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv));
    setsockopt(s, SOL_SOCKET, SO_SNDTIMEO, &tv, sizeof(tv));
#endif
}

bool SendAll(socket_t s, const char* data, size_t size)
{
    size_t sent = 0;
    while (sent < size)
    {
        auto n = ::send(s, data + sent, static_cast<int>(size - sent), 0);
        if (n <= 0)
        {
            return false;
        }
        sent += static_cast<size_t>(n);
    }
    return true;
}

// Parse "HTTP/1.1 200 OK" -> 200. Returns -1 on failure.
int ParseStatusCode(std::string const& statusLine)
{
    auto firstSpace = statusLine.find(' ');
    if (firstSpace == std::string::npos)
    {
        return -1;
    }

    auto codeStart = firstSpace + 1;
    auto codeEnd = statusLine.find(' ', codeStart);
    auto code = (codeEnd == std::string::npos)
        ? statusLine.substr(codeStart)
        : statusLine.substr(codeStart, codeEnd - codeStart);

    try
    {
        return std::stoi(code);
    }
    catch (...)
    {
        return -1;
    }
}

} // namespace

HttpClient::Response HttpClient::Post(
    std::string const& host,
    int port,
    std::string const& path,
    std::vector<Header> const& headers,
    std::vector<uint8_t> const& body,
    int timeoutMs)
{
    Response response;

#ifdef _WINDOWS
    EnsureWinsockInitialized();
#endif

    struct addrinfo hints;
    std::memset(&hints, 0, sizeof(hints));
    hints.ai_family = AF_UNSPEC;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;

    auto portStr = std::to_string(port);
    struct addrinfo* addresses = nullptr;
    if (::getaddrinfo(host.c_str(), portStr.c_str(), &hints, &addresses) != 0 || addresses == nullptr)
    {
        response.Error = "Unable to resolve agent host '" + host + ":" + portStr + "'";
        return response;
    }

    socket_t sock = InvalidSocket;
    for (auto* addr = addresses; addr != nullptr; addr = addr->ai_next)
    {
        sock = ::socket(addr->ai_family, addr->ai_socktype, addr->ai_protocol);
        if (sock == InvalidSocket)
        {
            continue;
        }

        SetTimeouts(sock, timeoutMs);

        if (::connect(sock, addr->ai_addr, static_cast<int>(addr->ai_addrlen)) == 0)
        {
            break;
        }

        CloseSocket(sock);
        sock = InvalidSocket;
    }

    ::freeaddrinfo(addresses);

    if (sock == InvalidSocket)
    {
        response.Error = "Unable to connect to agent '" + host + ":" + portStr + "': " + LastSocketError();
        return response;
    }

    std::ostringstream requestHead;
    requestHead << "POST " << path << " HTTP/1.1\r\n";
    requestHead << "Host: " << host << ":" << port << "\r\n";
    for (auto const& [name, value] : headers)
    {
        requestHead << name << ": " << value << "\r\n";
    }
    requestHead << "Content-Length: " << body.size() << "\r\n";
    requestHead << "Connection: close\r\n";
    requestHead << "\r\n";

    auto head = requestHead.str();
    if (!SendAll(sock, head.data(), head.size()) ||
        (!body.empty() && !SendAll(sock, reinterpret_cast<const char*>(body.data()), body.size())))
    {
        response.Error = "Failed to send profile to agent: " + LastSocketError();
        CloseSocket(sock);
        return response;
    }

    // Read the response. We only need the status line, but we keep reading a bit
    // to reach the end of the first line if it spans multiple recv() calls.
    std::string received;
    char buffer[2048];
    while (received.find("\r\n") == std::string::npos)
    {
        auto n = ::recv(sock, buffer, sizeof(buffer), 0);
        if (n <= 0)
        {
            break;
        }
        received.append(buffer, static_cast<size_t>(n));
        if (received.size() > 8192)
        {
            break;
        }
    }

    CloseSocket(sock);

    auto lineEnd = received.find("\r\n");
    auto statusLine = (lineEnd == std::string::npos) ? received : received.substr(0, lineEnd);
    auto statusCode = ParseStatusCode(statusLine);
    if (statusCode < 0)
    {
        response.Error = "Invalid or empty HTTP response from agent";
        return response;
    }

    response.Succeeded = true;
    response.StatusCode = statusCode;
    return response;
}
