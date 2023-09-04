#pragma once
#include <windows.h>
#include <stdint.h>


class IIpcEndpoint
{
public:
    virtual bool Write(LPCVOID buffer, DWORD bufferSize, DWORD* writtenBytes) = 0;
    virtual bool Read(LPVOID buffer, DWORD bufferSize, DWORD* readBytes) = 0;
    virtual bool ReadByte(uint8_t& byte) = 0;
    virtual bool ReadWord(uint16_t& word) = 0;
    virtual bool ReadDWord(uint32_t& dword) = 0;
    virtual bool ReadLong(uint64_t& ulong) = 0;

    virtual bool Close() = 0;
    virtual ~IIpcEndpoint() = default;
};

