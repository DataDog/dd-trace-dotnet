#pragma once

#include <corhlpr.h>
#include <memory>

class SignatureBuilder
{
public:
    SignatureBuilder();

    // _buffer points into this object while the signature fits in _stackSignatureBuffer, so a
    // copied or moved instance would keep pointing at the original. Nothing needs either.
    SignatureBuilder(const SignatureBuilder&) = delete;
    SignatureBuilder& operator=(const SignatureBuilder&) = delete;
    SignatureBuilder(SignatureBuilder&&) = delete;
    SignatureBuilder& operator=(SignatureBuilder&&) = delete;

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

    void EnsureBufferSpace(size_t size);
};
