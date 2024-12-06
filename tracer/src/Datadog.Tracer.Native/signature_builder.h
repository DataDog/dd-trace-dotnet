#pragma once

#include <corhlpr.h>
#include <memory>

class SignatureBuilder
{
public:
    SignatureBuilder();

    void Append(const COR_SIGNATURE elementType);
    void Append(const void* elements, const size_t length);

    const size_t Size() const { return _offset; }

    const COR_SIGNATURE* GetSignature() const { return _buffer; }

private:
    constexpr static int STACK_BUFFER_SIZE = 1000;

    COR_SIGNATURE _stackSignatureBuffer[STACK_BUFFER_SIZE];
    std::unique_ptr<COR_SIGNATURE[]> _heapSignatureBuffer;

    COR_SIGNATURE* _buffer;

    size_t _length;
    size_t _offset;

    void EnsureBufferSpace(int size);
};
