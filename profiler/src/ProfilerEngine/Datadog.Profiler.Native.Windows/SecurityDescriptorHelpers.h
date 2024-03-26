// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <aclapi.h>
#include <memory>
#include <string>


struct emptySA_deleter
{
    void operator()(SECURITY_ATTRIBUTES* ptr) const
    {
        if (ptr->lpSecurityDescriptor == nullptr)
        {
            return;
        }

        SECURITY_DESCRIPTOR* pSD = (SECURITY_DESCRIPTOR*)ptr->lpSecurityDescriptor;
        // just in case, a Dacl would have been allocated

        ::HeapFree(GetProcessHeap(), 0, pSD);
        ptr->lpSecurityDescriptor = nullptr;

        HeapFree(GetProcessHeap(), 0, ptr);
    }
};
typedef std::unique_ptr<SECURITY_ATTRIBUTES, emptySA_deleter> empty_sa_ptr;

empty_sa_ptr MakeNoSecurityAttributes(std::string& errorMessage);

