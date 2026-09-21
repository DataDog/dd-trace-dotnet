// We don't compile the vendored shared/src/native-lib/dotnet-runtime/minipal/guid.c: its
// minipal_guid_v4_create (which we never call - we only compare GUIDs, never generate them)
// pulls in minipal/random.c, which needs upstream's generated minipalconfig.h
// (HAVE_GETRANDOM/HAVE_ARC4RANDOM_BUF/HAVE_BCRYPT_H/... capability probing) that our build
// doesn't produce. Provide just the one function we actually need instead, identical to
// minipal/guid.c's own implementation. See shared/src/native-lib/dotnet-runtime/README.md.
#ifndef _WIN32

#include <minipal/guid.h>

#include <cstring>

bool minipal_guid_equals(GUID const* g1, GUID const* g2)
{
    return memcmp(g1, g2, sizeof(GUID)) == 0;
}

#endif // _WIN32
