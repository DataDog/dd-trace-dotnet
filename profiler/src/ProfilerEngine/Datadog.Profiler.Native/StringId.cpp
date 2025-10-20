#include "StringId.h"

#include <string.h>

extern "C" {
    #include "datadog/common.h"
}
namespace libdatadog {
    //static_assert(sizeof(StringId) <= sizeof(ddog_prof_StringId), "StringId must be 64 bytes");

    template <size_t Expected, size_t Actual>
    struct CheckSize {
        static_assert(Expected <= Actual, "Size mismatch");
    };

    template <size_t Expected, size_t Actual>
    struct CheckAlignment {
        static_assert(Actual == Expected, "Alignment mismatch");
    };

    CheckSize<sizeof(ddog_prof_StringId), sizeof(StringId)> check;
    CheckAlignment<alignof(ddog_prof_StringId), alignof(StringId)> check2;

    StringId::StringId() {
        memset(storage, 0xFF, BufferSize);
    }

    StringId::operator bool() const noexcept {
        static_assert(BufferSize == 8, "BufferSize is not 8 bytes, the code below should be updated");
        const uint64_t full_ones = ~static_cast<uint64_t>(0);
        const uint64_t *value = reinterpret_cast<const uint64_t *>(storage);
        return (*value != full_ones);
    }
}