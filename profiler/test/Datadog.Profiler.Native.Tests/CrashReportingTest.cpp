// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "unknwn.h"

const IID IID_IUnknown = {0x00000000,
    0x0000,
    0x0000,
    {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};
    
#include "CrashReporting.h"

#ifdef _WIN32

#include "resource.h"
#include <string_view>
#include <windows.h>
#include <vector>
#include <iostream>
#include <profiler/src/ProfilerEngine/Datadog.Profiler.Native.Windows/CrashReportingWindows.h>

std::vector<BYTE> LoadEmbeddedDll(int resourceId)
{
    // Get the instance handle of the current module
    auto hModule = GetModuleHandle(NULL);

    if (hModule == NULL)
    {
        std::cerr << "Failed to get module handle." << std::endl;
        return {};
    }

    // Find the resource in the current module
    auto hRes = FindResource(hModule, MAKEINTRESOURCE(resourceId), L"BINARY");
    
    if (hRes == NULL)
    {
        std::cerr << "Failed to find resource." << std::endl;
        return {};
    }

    // Load the resource into memory
    auto hResData = LoadResource(hModule, hRes);

    if (hResData == NULL)
    {
        std::cerr << "Failed to load resource." << std::endl;
        return {};
    }

    // Get a pointer to the resource data
    auto data = LockResource(hResData);

    // We can't just directly return a pointer to the binary data.
    // When a DLL is loaded, its sections are relocated at different addresses.
    // Because the code to parse the PE header expects those sections to be properly relocated,
    // we need to simulate the work of the loader.
    auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(data);
    uintptr_t ntHeadersAddress = (uintptr_t)data + dosHeader->e_lfanew;
    auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS_GENERIC*>(ntHeadersAddress);
    auto sectionHeaderAddress = ntHeadersAddress + FIELD_OFFSET(IMAGE_NT_HEADERS, OptionalHeader) + ntHeaders->FileHeader.SizeOfOptionalHeader;
    auto sectionHeaders = reinterpret_cast<IMAGE_SECTION_HEADER*>(sectionHeaderAddress);

    // Compute the needed size for the relocated image by computing the end of the farthest section
    SIZE_T size = 0;

    for (int i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++)
    {
        auto section = &sectionHeaders[i];
        auto sectionEnd = section->VirtualAddress + max(section->SizeOfRawData, section->Misc.VirtualSize);

        if (sectionEnd > size)
        {
            size = sectionEnd;
        }
    }

    // Allocate memory for the relocated image
    std::vector<BYTE> relocatedImage(size);

    // Copy the DOS and NT headers, and the section headers
    auto sizeToCopy = dosHeader->e_lfanew // Offset to the NT headers
        + FIELD_OFFSET(IMAGE_NT_HEADERS, OptionalHeader) + ntHeaders->FileHeader.SizeOfOptionalHeader // Optional header
        + ntHeaders->FileHeader.NumberOfSections * sizeof(IMAGE_SECTION_HEADER); // Section headers

    memcpy(relocatedImage.data(), data, sizeToCopy);

    // Copy the relocated sections
    for (int i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++)
    {
        PIMAGE_SECTION_HEADER section = &sectionHeaders[i];
        SIZE_T sectionEnd = section->VirtualAddress + max(section->SizeOfRawData, section->Misc.VirtualSize);

        memcpy(relocatedImage.data() + section->VirtualAddress, (void*)((uintptr_t)data + section->PointerToRawData), section->SizeOfRawData);
    }

    return relocatedImage;
}

std::vector<BYTE> ReadInProcessMemory(uintptr_t address, SIZE_T size)
{
    return std::vector<BYTE>((BYTE*)address, (BYTE*)address + size);
}

TEST(CrashReportingTest, ExtractPdbSignaturePE32)
{
    auto decoded_data = LoadEmbeddedDll(IDR_DATADOG_TRACE_MANUAL); // Datadog.Trace.Manual.dll v3.3.1
    ASSERT_GT(decoded_data.size(), 0);

    ModuleInfo moduleInfo{};

    CrashReportingWindows crashReporting(0);
    crashReporting.SetMemoryReader(ReadInProcessMemory);
    auto buildId = crashReporting.ExtractBuildId((uintptr_t)decoded_data.data());

    std::string_view buildIdStr = buildId;
    // Pdb signature C11BDBD67F764D72849D12DDB8E49E3F
    // Age           1
    //                                  |        Pdb signature          |Age|
    ASSERT_STRCASEEQ(buildIdStr.data(), "C11BDBD67F764D72849D12DDB8E49E3F1");
}

TEST(CrashReportingTest, ExtractPdbSignaturePE64)
{
    auto decoded_data = LoadEmbeddedDll(IDR_SFC); // sfc.dll v10.0.19041.4842
    ASSERT_GT(decoded_data.size(), 0);

    ModuleInfo moduleInfo{};

    CrashReportingWindows crashReporting(0);
    crashReporting.SetMemoryReader(ReadInProcessMemory);
    auto buildId = crashReporting.ExtractBuildId((uintptr_t)decoded_data.data());

    std::string_view buildIdStr = buildId;
    
    // Pdb signature C465AFCDBDBC58A0100995839A0E4C27
    // Age           1
    //                                  |        Pdb signature          |Age|
    ASSERT_STRCASEEQ(buildIdStr.data(), "C465AFCDBDBC58A0100995839A0E4C271");
}
#endif

TEST(CrashReportingTest, CheckMergedCallstackOnAlternateStackWithHighAddresses)
{
    std::vector<StackFrame> nativeFrames = {
        // next two frames simulate signal handler runnin on alternate stack
        {0x7F4DECDF2BC0, 0x7F478000ACE0, "__GI___wait4", 0x7F4DECDF2BC0, 0x7F4DECD1F000, false, ""},
        {0x7F4DECB882F0, 0x7F478000AD10, "PROCCreateCrashDump(std::vector<char const*, std::allocator<char const*> >&, char*, int, bool)", 0x7F4DECB882F0, 0x7F4DEC514000, false, ""},
        // below managed before the signal handler
        {0x7F4D778E1A7D, 0x7F476CC3E9D0, "/memfd:doublemapper (deleted)!<unknown>+b84da7d", 0x7F4D778E1A7D, 0x7F4D6C094000, false, ""},
        {0x7F4D76593D2A, 0x7F476CC3EA10, "/memfd:doublemapper (deleted)!<unknown>+a4ffd2a", 0x7F4D76593D2A, 0x7F4D6C094000, false, ""},
        {0x7F4D6D7D3924, 0x7F476CC3EA90, "/usr/share/dotnet/shared/Microsoft.NETCore.App/9.0.10/System.Private.CoreLib.dll!<unknown>+5e370b924", 0x7F4D6D7D3924, 0x7F478A0C8000, false, ""},
    };

    std::vector<StackFrame> managedFrames = {
        {0x7F4D778E1A7D, 0x7F476CC3E9D0, "System.Private.CoreLib.dll!System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238<System.__Canon>>.MoveNext", 0x7F4D778E1A7D, 0x7F4D6C094000, false, ""},
        {0x7F4D76593D2A, 0x7F476CC3EA10, "System.Private.CoreLib.dll!System.Threading.ThreadPoolWorkQueue.Dispatch", 0x7F4D76593D2A, 0x7F4D6C094000, false, ""},
        {0x7F4D6D7D3924, 0x7F476CC3EA90, "System.Private.CoreLib.dll!System.Threading.PortableThreadPool+WorkerThread.WorkerThreadStart", 0x7F4D6D7D3924, 0x7F478A0C8000, false, ""},
    };

    // MergeFrames returns the frames in the order of the sp addresses
    auto mergedFrames = CrashReporting::MergeFrames(nativeFrames, managedFrames);
    
    std::vector<std::string> expectedFunctions = {
        "System.Private.CoreLib.dll!System.Threading.PortableThreadPool+WorkerThread.WorkerThreadStart",
        "System.Private.CoreLib.dll!System.Threading.ThreadPoolWorkQueue.Dispatch",
        "System.Private.CoreLib.dll!System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238<System.__Canon>>.MoveNext",
        "PROCCreateCrashDump(std::vector<char const*, std::allocator<char const*> >&, char*, int, bool)",
        "__GI___wait4",
    };
    
    ASSERT_EQ(mergedFrames.size(), expectedFunctions.size());
    for (size_t i = 0; i < mergedFrames.size(); i++)
    {
        ASSERT_EQ(mergedFrames[i].method, expectedFunctions[i]);
    }
}

TEST(CrashReportingTest, CheckMergedCallstackOnAlternateStackWithLowAddresses)
{
    std::vector<StackFrame> nativeFrames = {
        // next two frames simulate signal handler runnin on alternate stack
        {0x7F4DECDF2BC0, 0x7F470000ACE0, "__GI___wait4", 0x7F4DECDF2BC0, 0x7F4DECD1F000, false, ""},
        {0x7F4DECB882F0, 0x7F470000AD10, "PROCCreateCrashDump(std::vector<char const*, std::allocator<char const*> >&, char*, int, bool)", 0x7F4DECB882F0, 0x7F4DEC514000, false, ""},
        // below managed before the signal handler
        {0x7F4D778E1A7D, 0x7F476CC3E9D0, "/memfd:doublemapper (deleted)!<unknown>+b84da7d", 0x7F4D778E1A7D, 0x7F4D6C094000, false, ""},
        {0x7F4D76593D2A, 0x7F476CC3EA10, "/memfd:doublemapper (deleted)!<unknown>+a4ffd2a", 0x7F4D76593D2A, 0x7F4D6C094000, false, ""},
        {0x7F4D6D7D3924, 0x7F476CC3EA90, "/usr/share/dotnet/shared/Microsoft.NETCore.App/9.0.10/System.Private.CoreLib.dll!<unknown>+5e370b924", 0x7F4D6D7D3924, 0x7F478A0C8000, false, ""},
    };

    std::vector<StackFrame> managedFrames = {
        {0x7F4D778E1A7D, 0x7F476CC3E9D0, "System.Private.CoreLib.dll!System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238<System.__Canon>>.MoveNext", 0x7F4D778E1A7D, 0x7F4D6C094000, false, ""},
        {0x7F4D76593D2A, 0x7F476CC3EA10, "System.Private.CoreLib.dll!System.Threading.ThreadPoolWorkQueue.Dispatch", 0x7F4D76593D2A, 0x7F4D6C094000, false, ""},
        {0x7F4D6D7D3924, 0x7F476CC3EA90, "System.Private.CoreLib.dll!System.Threading.PortableThreadPool+WorkerThread.WorkerThreadStart", 0x7F4D6D7D3924, 0x7F478A0C8000, false, ""},
    };

    auto mergedFrames = CrashReporting::MergeFrames(nativeFrames, managedFrames);

    std::vector<std::string> expectedFunctions = {
        "System.Private.CoreLib.dll!System.Threading.PortableThreadPool+WorkerThread.WorkerThreadStart",
        "System.Private.CoreLib.dll!System.Threading.ThreadPoolWorkQueue.Dispatch",
        "System.Private.CoreLib.dll!System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238<System.__Canon>>.MoveNext",
        "PROCCreateCrashDump(std::vector<char const*, std::allocator<char const*> >&, char*, int, bool)",
        "__GI___wait4",
    };

    ASSERT_EQ(mergedFrames.size(), expectedFunctions.size());
    for (size_t i = 0; i < mergedFrames.size(); i++)
    {
        ASSERT_EQ(mergedFrames[i].method, expectedFunctions[i]);
    }
}

TEST(CrashReportingTest, CheckMergedCallstackButNoFusionBetweenNativeAndManaged)
{
    std::vector<StackFrame> nativeFrames = {
        {0x7F4DEC9B2982, 0x7F476CC39E90, "MethodTable::GetFlag(MethodTable::WFLAGS_HIGH_ENUM) const", 0x7F4D778E1A7D, 0x7F4D6C094000, false, ""},
        {0x7F4DEC9B3233, 0x7F476CC39F10, "WKS::gc_heap::mark_object_simple(unsigned char**)", 0x7F4D76593D2A, 0x7F4D6C094000, false, ""},
        {0x7F4DEC9B7929, 0x7F476CC39F70, "WKS::gc_heap::mark_through_cards_helper(unsigned char**, unsigned long&, unsigned long&, void (*)(unsigned char**), unsigned char*, unsigned char*, int, int)", 0x7F4D6D7D3924, 0x7F478A0C8000, false, ""},
    };

    std::vector<StackFrame> managedFrames = {
        {0x7F4D778E1A7D, 0x7F476CC3E9D0, "System.Private.CoreLib.dll!System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238<System.__Canon>>.MoveNext", 0x7F4D778E1A7D, 0x7F4D6C094000, false, ""},
        {0x7F4D76593D2A, 0x7F476CC3EA10, "System.Private.CoreLib.dll!System.Threading.ThreadPoolWorkQueue.Dispatch", 0x7F4D76593D2A, 0x7F4D6C094000, false, ""},
        {0x7F4D6D7D3924, 0x7F476CC3EA90, "System.Private.CoreLib.dll!System.Threading.PortableThreadPool+WorkerThread.WorkerThreadStart", 0x7F4D6D7D3924, 0x7F478A0C8000, false, ""},
    };

    auto mergedFrames = CrashReporting::MergeFrames(nativeFrames, managedFrames);

    std::vector<std::string> expectedFunctions = {
        "System.Private.CoreLib.dll!System.Threading.PortableThreadPool+WorkerThread.WorkerThreadStart",
        "System.Private.CoreLib.dll!System.Threading.ThreadPoolWorkQueue.Dispatch",
        "System.Private.CoreLib.dll!System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>+AsyncStateMachineBox<Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238<System.__Canon>>.MoveNext",
        "WKS::gc_heap::mark_through_cards_helper(unsigned char**, unsigned long&, unsigned long&, void (*)(unsigned char**), unsigned char*, unsigned char*, int, int)",
        "WKS::gc_heap::mark_object_simple(unsigned char**)",
        "MethodTable::GetFlag(MethodTable::WFLAGS_HIGH_ENUM) const",
    };

    ASSERT_EQ(mergedFrames.size(), expectedFunctions.size());
    for (size_t i = 0; i < mergedFrames.size(); i++)
    {
        ASSERT_EQ(mergedFrames[i].method, expectedFunctions[i]);
    }
}