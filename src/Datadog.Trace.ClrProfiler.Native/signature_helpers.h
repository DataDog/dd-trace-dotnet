#pragma once

#include <cor.h>
#include <corhlpr.h>
#include "com_ptr.h"
#include "string.h"

#include <functional>

namespace trace {
// This limit is only for C# and not for the CLI, but it is good enough
const size_t MAX_IDENTIFIER_LENGTH = 511;
using identifier = std::array<wchar_t, MAX_IDENTIFIER_LENGTH + 1>;
static WSTRING SignatureToWSTRING(
    const ComPtr<IMetaDataImport2>& metadata_import, PCCOR_SIGNATURE);
};  // namespace trace
}  // namespace trace
