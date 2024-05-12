#!/bin/bash

# Safety checks to make sure we have required values
if [ -z "$CI_COMMIT_TAG" ]; then
  echo "Error: CI_COMMIT_TAG was not provided"
  exit 1
fi

if [ -z "$CI_COMMIT_SHA" ]; then
  echo "Error: CI_COMMIT_SHA was not provided"
  exit 1
fi

if [ -z "$IMG_SOURCES_BASE" ]; then
  echo "Error: IMG_SOURCES_BASE. This should be set to the full source docker image, excluding the tag name, e.g. ghcr.io/datadog/dd-trace-dotnet/dd-lib-dotnet-init"
  exit 1
fi

if [ -z "$IMG_DESTINATION_BASE" ]; then
  echo "Error: IMG_DESTINATION_BASE. This should be set to the destination docker image, excluding the tag name, e.g. dd-lib-dotnet-init"
  exit 1
fi

if [ -z "$PUBLIC_IMAGES_PIPELINE_PROJECT" ]; then
  echo "Error: PUBLIC_IMAGES_PIPELINE_PROJECT. This should be set to the public-images repo, DataDog/public-images, unless you're testing"
  exit 1
fi

if [ -z "$INCLUDE_MUSL" ]; then
  INCLUDE_MUSL=
else
  if [ "$INCLUDE_MUSL" -ne 0 ] && [ "$INCLUDE_MUSL" -ne 1 ]; then
    echo "Error: INCLUDE_MUSL should be set to 0 or 1"
    exit 1
  fi
fi

# If this is a pre-release release, we skip all the additional checks 
if echo "$CI_COMMIT_TAG" | grep -q "-" > /dev/null; then
  echo "This is a pre-release release version"
  IS_PRERELEASE=1
  MAJOR_MINOR_VERSION=""
  MAJOR_VERSION=""
  IS_LATEST_TAG=0
  IS_LATEST_MAJOR_TAG=0
else
  IS_PRERELEASE=0

  # There are other ways we could get all tags (e.g. git tag) which are arguably preferable,
  # but can't guarantee we're running a checked out git repo (apparently)
  $allTags=


  # We need to determine whether this is is the latest tag and whether it's the latest major or not
  # So we fetch all tags from GitHub and sort them to find both the latest, and the latest in this major.
  # sort actually gets prerelease versions in technically the wrong order here
  # but we explicitly include them anyway, as we don't want to add any of the floating tags
  # to prerelease versions.

  LATEST_TAG="$(git tag | grep -v '-' | sort -V -r | head -n 1)"
  LATEST_MAJOR_TAG="$(git tag -l "$MAJOR_VERSION.*" | grep -v '-' | sort -V -r | head -n 1)"
  echo "This tag: $CI_COMMIT_TAG"
  echo "Latest repository tag: $LATEST_TAG"
  echo "Latest repository tag for this major: $LATEST_MAJOR_TAG"
  
  is_greater_than_or_equal() {
    # GNU sort -C (silent) reports via exit code whether
    # the data is already in sorted order
    printf '%s\n' "$2" "$1" | sort -C -V
  }
  
  if is_greater_than_or_equal "$CI_COMMIT_TAG" "$LATEST_TAG"; then
    # The current tag is the latest in the repository
    IS_LATEST_TAG=1
  else
    IS_LATEST_TAG=0
  fi
  
  if is_greater_than_or_equal "$CI_COMMIT_TAG" "$LATEST_MAJOR_TAG"; then
    # The current tag is the latest for this major version in the repository
    IS_LATEST_MAJOR_TAG=1
  else
    IS_LATEST_MAJOR_TAG=0
  fi
  
  # Calculate the tags we use for floating major and minor versions
  MAJOR_MINOR_VERSION="$(sed -nE 's/^(v[0-9]+\.[0-9]+)\.[0-9]+$/\1/p' <<< ${CI_COMMIT_TAG})"
  MAJOR_VERSION="$(sed -nE 's/^(v[0-9]+)\.[0-9]+\.[0-9]+$/\1/p' <<< ${CI_COMMIT_TAG})"
fi

# print everything for debugging purposes
echo "MAJOR_MINOR_VERSION=${MAJOR_MINOR_VERSION}"
echo "MAJOR_VERSION=${MAJOR_VERSION}"
echo "IS_LATEST_TAG=${IS_LATEST_TAG}"
echo "IS_LATEST_MAJOR_TAG=${IS_LATEST_MAJOR_TAG}"
echo "IS_PRERELEASE=${IS_PRERELEASE}"

# Final check that everything is ok
# if this is non-prerelease, we should have a major_minor version
if [ "$IS_PRERELEASE" -eq 0 ] && [ -z "$MAJOR_MINOR_VERSION" ]; then
  echo "Error: Could not determine major_minor version for stable release, this should not happen"
  exit 1
fi

# if this is a latest major tag, we should have a major version
if [ "$IS_LATEST_MAJOR_TAG" -eq 1 ] && [ -z "$MAJOR_VERSION" ]; then
  echo "Error: Could not determine major version for latest major release, this should not happen"
  exit 1
fi

# Helper functions for building the script
add_stage() {
  STAGE_NAME="$1"
  DEST_TAG="$2"
  TAG_SUFFIX="$3"
  
  cat << EOF >> generated-config.yml
deploy_${STAGE_NAME}_docker:
  stage: trigger-public-images
  trigger:
    project: $PUBLIC_IMAGES_PIPELINE_PROJECT
    branch: main
    strategy: depend
  variables:
    IMG_SOURCES: $IMG_SOURCES_BASE:$CI_COMMIT_SHA
    IMG_DESTINATIONS: $IMG_DESTINATION_BASE:$DEST_TAG$TAG_SUFFIX
    IMG_SIGNING: "false"
    
EOF
}

add_all_stages() {
  SUFFIX="$1"
  STAGE_SUFFIX="${SUFFIX:+_$SUFFIX}"
  TAG_SUFFIX="${SUFFIX:+-$SUFFIX}"
  
  # We always add this tag, regardless of the version 
  add_stage "major_minor_patch$STAGE_SUFFIX" $CI_COMMIT_TAG $TAG_SUFFIX
  
  # If this is a pre-release version, we don't add the other stages
  if [ "$IS_PRERELEASE" -eq 1 ]; then
    return
  fi

  # All non-prerelease stages get the major_minor tag
  add_stage "major_minor$STAGE_SUFFIX" $MAJOR_MINOR_VERSION $TAG_SUFFIX

  # Only latest-major releases get the major tag
  if [ "$IS_LATEST_MAJOR_TAG" -eq 1 ]; then
    add_stage "major$STAGE_SUFFIX" $MAJOR_VERSION $TAG_SUFFIX
  fi
  
  # Only latest releases get the latest tag
  if [ "$IS_LATEST_TAG" -eq 1 ]; then
    add_stage "major$STAGE_SUFFIX" "latest" $TAG_SUFFIX
  fi
}

# Build the script


# Generate the pipeline for triggering child jobs
cat << EOF > generated-config.yml
stages:
  - trigger-public-images

EOF

# Append the non-suffixed stages
add_all_stages

if [ "$INCLUDE_MUSL" -eq 1 ]; then
  add_all_stages "musl"
fi

# All finished - print the generated yaml for debugging purposes
echo "Generated pipeline:"
cat generated-config.yml