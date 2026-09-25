#pragma once

// Helpers for hand-building signature blobs in tests.
//
// Deliberately kept out of test_helpers.h: that header hosts the CLR to open a metadata scope,
// which is Windows-only, and these helpers are used from runtime_async_test.cpp, which builds on
// every platform.

#include "../../src/Datadog.Tracer.Native/clr_helpers.h"

#include <vector>

namespace trace
{

class SigBuilder
{
public:
    SigBuilder& Byte(COR_SIGNATURE b)
    {
        _bytes.push_back(b);
        return *this;
    }

    SigBuilder& Token(mdToken token)
    {
        COR_SIGNATURE buffer[8]{};
        const auto written = CorSigCompressToken(token, buffer);
        for (ULONG i = 0; i < written; i++)
        {
            _bytes.push_back(buffer[i]);
        }
        return *this;
    }

    // Emits `count` filler bytes, so tests can place the return type somewhere other than offset 0
    // and prove the offsets we hand back are relative to pbBase rather than to the return type.
    SigBuilder& Filler(size_t count)
    {
        for (size_t i = 0; i < count; i++)
        {
            _bytes.push_back(0xEE);
        }
        return *this;
    }

    const std::vector<COR_SIGNATURE>& Bytes() const
    {
        return _bytes;
    }

private:
    std::vector<COR_SIGNATURE> _bytes;
};

// Builds a TypeSignature covering everything from `offset` to the end of the blob.
inline TypeSignature Sig(const std::vector<COR_SIGNATURE>& bytes, ULONG offset = 0)
{
    return TypeSignature{offset, static_cast<ULONG>(bytes.size()) - offset, bytes.data()};
}

} // namespace trace
