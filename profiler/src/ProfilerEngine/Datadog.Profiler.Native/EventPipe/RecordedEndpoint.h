#pragma once

#include "IIpcEndpoint.h"

class RecordedEndpoint : public IIpcEndpoint
{
public:
    static RecordedEndpoint* Create(const wchar_t* recordFilename);

    // Inherited via IIpcEndpoint
    // NOP operation
    virtual bool Write(LPCVOID buffer, DWORD bufferSize, DWORD* writtenBytes) override;

    // read from the recorded file
    virtual bool Read(LPVOID buffer, DWORD bufferSize, DWORD* readBytes) override;
    virtual bool ReadByte(uint8_t& byte) override;
    virtual bool ReadWord(uint16_t& word) override;
    virtual bool ReadDWord(uint32_t& dword) override;
    virtual bool ReadLong(uint64_t& ulong) override;

   // cleanup
    virtual bool Close() override;

protected:
    ~RecordedEndpoint();

private:
    RecordedEndpoint();

private:
    HANDLE _hFile;
};

