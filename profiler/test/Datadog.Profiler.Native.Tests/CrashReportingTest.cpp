// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef _WIN32

#include "resource.h"
#include "gtest/gtest.h"
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