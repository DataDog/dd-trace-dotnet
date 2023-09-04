#pragma once
#include "IIpcRecorder.h"
class FileRecorder :
    public IIpcRecorder
{
public:
    FileRecorder(const wchar_t* filename);
    ~FileRecorder();

    virtual bool Write(LPCVOID buffer, DWORD bufferSize) override;
    virtual bool Close() override;


private:
    HANDLE _hFile;
};

