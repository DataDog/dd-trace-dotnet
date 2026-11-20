#!/usr/bin/env bash
set -euo pipefail

branch="$1"
sha="$2"
version_props="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_build/version.props"

branch="${branch#refs/heads/}"
case "$(echo "$branch" | tr '[:upper:]' '[:lower:]')" in
  ''|refs/*|master|main|release*|hotfix*)
    echo "Not setting a CI dev version suffix for branch '$branch'"
    exit 0
    ;;
esac

sha="$(echo "$sha" | tr '[:upper:]' '[:lower:]')"
if ! [[ "$sha" =~ ^[0-9a-f]{8,}$ ]]; then
  echo "Expected a commit SHA, but got '$sha'" >&2
  exit 1
fi

if ! grep -q '<DD_VersionSuffix>[^<]*</DD_VersionSuffix>' "$version_props"; then
  echo "Could not find <DD_VersionSuffix> in $version_props" >&2
  exit 1
fi

current="$(sed -n 's|.*<DD_VersionSuffix>\([^<]*\)</DD_VersionSuffix>.*|\1|p' "$version_props")"

if [ -n "$current" ]; then
  echo "Not setting a CI dev version suffix: the version suffix is already '$current'"
  exit 0
fi

suffix="-dev.ci.g${sha:0:8}"
sed -i.bak "s|<DD_VersionSuffix></DD_VersionSuffix>|<DD_VersionSuffix>${suffix}</DD_VersionSuffix>|" "$version_props"
rm -f "$version_props.bak"

echo "Set the version suffix to '$suffix' for branch '$branch'"
