#pragma once
#include <windows.h>

class IIpcRecorder
{
public:
    virtual bool Write(LPCVOID buffer, DWORD bufferSize) = 0;
    virtual bool Close() = 0;

    virtual ~IIpcRecorder() = default;
};
