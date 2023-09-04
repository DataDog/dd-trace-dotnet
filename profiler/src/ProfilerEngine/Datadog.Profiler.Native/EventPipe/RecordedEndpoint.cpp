#include <exception>
#include "Shlwapi.h"
#include "RecordedEndpoint.h"

RecordedEndpoint::RecordedEndpoint()
{
    _hFile = nullptr;
}

RecordedEndpoint::~RecordedEndpoint()
{
    Close();
}

RecordedEndpoint* RecordedEndpoint::Create(const wchar_t* recordFilename)
{
    auto pEndpoint = new RecordedEndpoint();
    pEndpoint->_hFile = ::CreateFile(recordFilename, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL || FILE_FLAG_SEQUENTIAL_SCAN, nullptr);
    if (pEndpoint->_hFile == INVALID_HANDLE_VALUE)
    {
        delete pEndpoint;
        return nullptr;
    }

    return pEndpoint;
}


bool RecordedEndpoint::Write(LPCVOID buffer, DWORD bufferSize, DWORD* writtenBytes)
{
    // NOP operation
    *writtenBytes = bufferSize;
    return true;
}

bool RecordedEndpoint::Read(LPVOID buffer, DWORD bufferSize, DWORD* readBytes)
{
    bool success = ::ReadFile(_hFile, buffer, bufferSize, readBytes, nullptr);

    // check specific case when reaching the end of the file (i.e. success and *readBytes == 0)
    if (success && (*readBytes == 0))
    {
        *readBytes = bufferSize;
    }

    return success;
}

bool RecordedEndpoint::ReadByte(uint8_t& byte)
{
    DWORD readBytes = 0;
    return Read(&byte, sizeof(byte), &readBytes);
}

bool RecordedEndpoint::ReadWord(uint16_t& word)
{
    DWORD readBytes = 0;
    return Read(&word, sizeof(word), &readBytes);
}

bool RecordedEndpoint::ReadDWord(uint32_t& dword)
{
    DWORD readBytes = 0;
    return Read(&dword, sizeof(dword), &readBytes);
}

bool RecordedEndpoint::ReadLong(uint64_t& ulong)
{
    DWORD readBytes = 0;
    return Read(&ulong, sizeof(ulong), &readBytes);
}

bool RecordedEndpoint::Close()
{
    if (_hFile == INVALID_HANDLE_VALUE)
        return false;

    ::CloseHandle(_hFile);
    _hFile = INVALID_HANDLE_VALUE;

    return true;
}
