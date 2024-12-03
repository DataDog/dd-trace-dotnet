#include "signature_builder.h"

SignatureBuilder::SignatureBuilder()
{
    _buffer = _stackSignatureBuffer;
    _offset = 0;
    _length = STACK_BUFFER_SIZE;
}

void SignatureBuilder::Append(const COR_SIGNATURE element)
{
    EnsureBufferSpace(1);
    _buffer[_offset++] = element;
}

void SignatureBuilder::Append(const void* elements, const size_t length)
{
    EnsureBufferSpace(length);
    memcpy(_buffer + _offset, elements, length);
    _offset += length;
}

void SignatureBuilder::EnsureBufferSpace(int size)
{
    if (_offset + size >= _length)
    {
        auto newSignatureBuffer = std::make_unique<COR_SIGNATURE[]>(_length * 2);
        memcpy(newSignatureBuffer.get(), _buffer, _length);
        _heapSignatureBuffer = std::move(newSignatureBuffer);
        _buffer = _heapSignatureBuffer.get();
        _length *= 2;
    }
}
