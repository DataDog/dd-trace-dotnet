// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SecurityDescriptorHelpers.h"


// As the documentation states, default permissions are too restrictive since the Agent user won't be able
// to access the named pipe if a NULL security attributes is passed.
// Instead, create a security attributes with NULL Dacl (see https://techcommunity.microsoft.com/t5/ask-the-directory-services-team/null-and-empty-dacls/ba-p/396323 for more details)
//
bool FillNoSecurityAttributes(SECURITY_ATTRIBUTES* pSA, std::string& errorMessage)
{
    ::ZeroMemory(pSA, sizeof(SECURITY_ATTRIBUTES));
    pSA->nLength = sizeof(SECURITY_ATTRIBUTES);
    pSA->bInheritHandle = FALSE;

    SECURITY_DESCRIPTOR* pSD = (SECURITY_DESCRIPTOR*)::HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, SECURITY_DESCRIPTOR_MIN_LENGTH);
    pSA->lpSecurityDescriptor = pSD;
    if (!InitializeSecurityDescriptor(pSD, SECURITY_DESCRIPTOR_REVISION))
    {
        errorMessage = "Failed to InitializeSecurityDescriptor...";
        return false;
    }

    if (!SetSecurityDescriptorDacl(pSD, TRUE, NULL, FALSE))
    {
        errorMessage = "Failed to SetSecurityDescriptorDacl...";
        return false;
    }

    return true;
}

empty_sa_ptr MakeNoSecurityAttributes(std::string& errorMessage)
{
    SECURITY_ATTRIBUTES* pSA = (SECURITY_ATTRIBUTES*)::HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, SECURITY_DESCRIPTOR_MIN_LENGTH);
    empty_sa_ptr sa = empty_sa_ptr(pSA);

    if (!FillNoSecurityAttributes(sa.get(), errorMessage))
    {
        return nullptr;
    }

    return sa;
}


