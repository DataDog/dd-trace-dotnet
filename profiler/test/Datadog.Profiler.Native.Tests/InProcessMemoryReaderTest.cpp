// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "InProcessMemoryReader.h"

#include <cstdint>

#ifdef LINUX
#include <sys/mman.h>
#include <unistd.h>
#endif

#ifdef _WINDOWS
#include <Windows.h>
#endif

namespace
{
#if defined(_WINDOWS)
void* MapInaccessiblePage()
{
    SYSTEM_INFO si{};
    GetSystemInfo(&si);
    const size_t pageSize = si.dwPageSize != 0 ? static_cast<size_t>(si.dwPageSize) : 4096;
    return VirtualAlloc(nullptr, pageSize, MEM_COMMIT | MEM_RESERVE, PAGE_NOACCESS);
}

void UnmapPage(void* p)
{
    if (p != nullptr)
    {
        VirtualFree(p, 0, MEM_RELEASE);
    }
}
#elif defined(LINUX)
void* MapInaccessiblePage()
{
    const long pageSize = sysconf(_SC_PAGESIZE);
    void* p = mmap(nullptr, static_cast<size_t>(pageSize), PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    return p == MAP_FAILED ? nullptr : p;
}

void UnmapPage(void* p)
{
    if (p != nullptr)
    {
        const long pageSize = sysconf(_SC_PAGESIZE);
        munmap(p, static_cast<size_t>(pageSize));
    }
}
#endif
} // namespace

TEST(InProcessMemoryReaderTest, PointerSizeMatchesPlatform)
{
    InProcessMemoryReader reader;
    EXPECT_EQ(reader.PointerSize(), static_cast<int>(sizeof(void*)));
}

TEST(InProcessMemoryReaderTest, ReadsOwnStackMemory)
{
    InProcessMemoryReader reader;

    uint64_t source = 0x1122334455667788ULL;
    uint64_t destination = 0;
    ASSERT_TRUE(reader.ReadMemory(reinterpret_cast<uintptr_t>(&source),
                                  reinterpret_cast<uint8_t*>(&destination), sizeof(destination)));
    EXPECT_EQ(destination, source);

    // The templated convenience helper goes through the same guarded path.
    uint64_t viaHelper = 0;
    ASSERT_TRUE(reader.Read(reinterpret_cast<uintptr_t>(&source), viaHelper));
    EXPECT_EQ(viaHelper, source);
}

#if defined(_WINDOWS) || defined(LINUX)
TEST(InProcessMemoryReaderTest, ReadOfUnmappedPageReturnsFalseWithoutCrashing)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    InProcessMemoryReader reader;

    uint8_t buffer[16] = {};
    // The fault guard (SEH on Windows, SIGSEGV/SIGBUS on Linux) must turn the access violation into
    // a clean "false" rather than terminating the test process.
    EXPECT_FALSE(reader.ReadMemory(reinterpret_cast<uintptr_t>(badPage), buffer, sizeof(buffer)));

    // The reader stays usable afterwards.
    uint32_t value = 0xABCD;
    uint32_t readBack = 0;
    EXPECT_TRUE(reader.Read(reinterpret_cast<uintptr_t>(&value), readBack));
    EXPECT_EQ(readBack, value);

    UnmapPage(badPage);
}
#endif
