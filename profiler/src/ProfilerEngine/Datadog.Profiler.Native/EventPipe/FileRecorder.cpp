#include <assert.h>
#include <exception>
#include <Windows.h>
#include "FileRecorder.h"

FileRecorder::FileRecorder(const wchar_t* filename)
{
    _hFile = ::CreateFile(filename, GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (_hFile == INVALID_HANDLE_VALUE)
    {
        throw std::exception("Impossible to create file...");
    }
}

FileRecorder::~FileRecorder()
{
    auto success = Close();
    assert(success);
}

bool FileRecorder::Write(LPCVOID buffer, DWORD bufferSize)
{
    DWORD writtenBytes = 0;
    auto success = ::WriteFile(_hFile, buffer, bufferSize, &writtenBytes, nullptr);
    return (success && (writtenBytes == bufferSize));
}

bool FileRecorder::Close()
{
    if (_hFile != INVALID_HANDLE_VALUE)
    {
        ::CloseHandle(_hFile);
        _hFile = INVALID_HANDLE_VALUE;
        return true;
    }

    return false;
}
