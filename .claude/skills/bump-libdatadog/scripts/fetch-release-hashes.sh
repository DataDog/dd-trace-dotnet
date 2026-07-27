#!/usr/bin/env bash
set -euo pipefail

# Fetches SHA-256 and SHA-512 checksums from a libdatadog-dotnet GitHub release.
# Usage: ./fetch-release-hashes.sh <VERSION>
# Example: ./fetch-release-hashes.sh 1.3.5
#
# The version should NOT include the 'v' prefix.
# Checksums are published in the release notes — no need to download artifacts.
#
# NOTE: dd-trace-dotnet consumes the .NET-specific distribution
# DataDog/libdatadog-dotnet, NOT upstream DataDog/libdatadog. The release
# version here (e.g. 1.3.5) is libdatadog-dotnet's own version, which is
# distinct from the upstream libdatadog version it is built from.

VERSION="${1:?Usage: $0 <VERSION> (e.g. 1.3.5)}"
RELEASE_URL="https://api.github.com/repos/DataDog/libdatadog-dotnet/releases/tags/v${VERSION}"

echo "============================================"
echo "libdatadog-dotnet v${VERSION} — Fetching checksums"
echo "============================================"
echo ""

BODY=$(curl -fsSL "$RELEASE_URL" | jq -r '.body')

# The release body lists both SHA256 and SHA512 checksums. Rather than depend on
# the exact section headings, split by hash length: SHA-256 is 64 hex chars,
# SHA-512 is 128. This is robust to release-note formatting changes.
echo ">>> SHA-256 checksums (for build/cmake/FindLibdatadog.cmake) <<<"
echo ""
echo "$BODY" | grep -E '^[0-9a-f]{64} libdatadog-' || echo "(none found — check release notes format)"
echo ""

echo ">>> SHA-512 checksums (for build/vcpkg_local_ports/libdatadog/portfile.cmake) <<<"
echo ""
echo "$BODY" | grep -E '^[0-9a-f]{128} libdatadog-' || echo "(none found — check release notes format)"
echo ""

echo "============================================"
echo "File mapping:"
echo "============================================"
echo ""
echo "build/cmake/FindLibdatadog.cmake:"
echo "  LIBDATADOG_VERSION = \"v${VERSION}\""
echo "  SHA256_LIBDATADOG_ARM64      ← aarch64-apple-darwin"
echo "  SHA256_LIBDATADOG_X86_64     ← x86_64-apple-darwin"
echo "  SHA256_LIBDATADOG (aarch64 gnu)   ← aarch64-unknown-linux-gnu"
echo "  SHA256_LIBDATADOG (aarch64 musl)  ← aarch64-alpine-linux-musl"
echo "  SHA256_LIBDATADOG (x86_64 musl)   ← x86_64-alpine-linux-musl"
echo "  SHA256_LIBDATADOG (x86_64 gnu)    ← x86_64-unknown-linux-gnu"
echo ""
echo "build/vcpkg_local_ports/libdatadog/vcpkg.json:"
echo "  version-string = \"${VERSION}\""
echo ""
echo "build/vcpkg_local_ports/libdatadog/portfile.cmake:"
echo "  x64 LIBDATADOG_HASH ← libdatadog-x64-windows.zip SHA-512"
echo "  x86 LIBDATADOG_HASH ← libdatadog-x86-windows.zip SHA-512"
echo ""
echo "Done."
