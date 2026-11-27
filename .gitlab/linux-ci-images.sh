#!/bin/sh
# Shared image inventory and lifecycle. Source from the repository root.

linux_ci_architecture() {
    case "$(uname -m)" in
        x86_64) printf '%s\n' x64 ;;
        aarch64) printf '%s\n' arm64 ;;
        *) echo 'Unsupported Linux runner architecture' >&2; return 1 ;;
    esac
}

linux_ci_variants() {
    case "$1" in
        x64|arm64) ;;
        *) echo 'Unsupported Linux CI image architecture' >&2; return 1 ;;
    esac
    printf '%s\n' ubuntu/builder ubuntu/tester alpine/builder alpine/tester
    printf '%s\n' universal/builder
}

resolve_linux_ci_images() {
    LINUX_CI_IMAGE_BASE=${LINUX_CI_IMAGE_BASE:-registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-linux-ci}
    case "$LINUX_CI_IMAGE_BASE" in
        ''|*[!a-zA-Z0-9./:_-]*) echo 'Invalid Linux image repository' >&2; return 1 ;;
    esac
    LINUX_DOTNET_SDK_VERSION=$(sh .gitlab/get-dotnet-sdk-version.sh) || return 1
    # Reuse Azure's native-toolchain pin instead of maintaining another copy.
    LINUX_NATIVE_DOTNET_SDK_VERSION=$(awk '
        /^[[:space:]]*nativeBuildDotnetSdkVersion:/ { version = $2; sub(/\r$/, "", version); count++ }
        END {
            if (count != 1 || version !~ /^[0-9]+\.[0-9]+\.[0-9]+$/) exit 1
            print version
        }
    ' .azure-pipelines/ultimate-pipeline.yml) || return 1

    # NUKE source and restore inputs are excluded: jobs restore/build the current
    # checkout, using the image's package cache only as an optimization.
    inputs='global.json
.gitlab/linux-ci-images.sh
.gitlab/get-dotnet-sdk-version.sh
.gitlab/linux-ci-environment.dockerfile
.gitlab/linux-unit-tests.dockerfile
tracer/build/_build/.dockerignore
tracer/build/_build/docker/ubuntu.dockerfile
tracer/build/_build/docker/alpine.dockerfile
tracer/build/_build/docker/universal.dockerfile'
    checksums=$(
        for input in $inputs; do
            sha256sum "$input" || exit 1
        done
    ) || return 1
    digest=$(printf '%s\n' "$checksums" "$LINUX_DOTNET_SDK_VERSION" "$LINUX_NATIVE_DOTNET_SDK_VERSION" | sha256sum) || return 1
    LINUX_CI_IMAGE_HASH=$(printf '%s' "$digest" | cut -c 1-12)
    export LINUX_CI_IMAGE_BASE LINUX_CI_IMAGE_HASH LINUX_DOTNET_SDK_VERSION LINUX_NATIVE_DOTNET_SDK_VERSION

    printf 'LINUX_CI_IMAGE_BASE=%s\nLINUX_CI_IMAGE_HASH=%s\nLINUX_DOTNET_SDK_VERSION=%s\nLINUX_NATIVE_DOTNET_SDK_VERSION=%s\n' \
        "$LINUX_CI_IMAGE_BASE" "$LINUX_CI_IMAGE_HASH" "$LINUX_DOTNET_SDK_VERSION" "$LINUX_NATIVE_DOTNET_SDK_VERSION"
}

linux_ci_image() {
    image_base=$1
    image_role=$2
    image_arch=${3:-$(linux_ci_architecture)}
    supported_variants=$(linux_ci_variants "$image_arch") || return 1
    if ! printf '%s\n' "$supported_variants" | grep -Fxq -- "$image_base/$image_role"; then
        echo 'Unsupported Linux CI image variant' >&2
        return 1
    fi
    printf '%s:%s-%s-%s-%s\n' "${LINUX_CI_IMAGE_BASE:?Missing image metadata}" \
        "${LINUX_CI_IMAGE_HASH:?Missing image metadata}" "$image_base" "$image_role" "$image_arch"
}

check_linux_ci_images() {
    check_arch=$1
    check_variants=$(linux_ci_variants "$check_arch") || return 1
    check_failed=0
    for check_variant in $check_variants; do
        check_image=$(linux_ci_image "${check_variant%/*}" "${check_variant#*/}" "$check_arch") || return 1
        echo "Checking Linux CI image: $check_image"
        if ! docker manifest inspect "$check_image" >/dev/null; then
            echo "ERROR: Cannot access $check_image" >&2
            check_failed=1
        fi
    done
    if [ "$check_failed" -ne 0 ]; then
        echo 'This job only checks availability; retrying it does NOT build or publish images.' >&2
        echo "If images are missing, review the changes and manually run the matching toolchain entries under 'build-linux-ci-images-$check_arch' in the root pipeline." >&2
        echo "Wait for publication, retry 'check-linux-ci-images-$check_arch', then retry any skipped consumers." >&2
        echo 'For authentication/network errors, resolve those before retrying; rebuilding may not be needed.' >&2
        return 1
    fi
}

pull_linux_ci_image() {
    pull_arch=$(linux_ci_architecture) || return 1
    pull_image=$(linux_ci_image "$1" "$2" "$pull_arch") || return 1
    if ! docker pull "$pull_image" >&2; then
        echo "Cannot pull Linux CI image: $pull_image" >&2
        echo "If it is missing, review the image changes and manually run 'build-linux-ci-images-$pull_arch: [$1]' in the root pipeline, then retry this job." >&2
        echo 'This job never builds or publishes images. For authentication/network errors, resolve those before retrying; rebuilding may not be needed.' >&2
        return 1
    fi
    printf '%s\n' "$pull_image"
}

publish_linux_ci_images() {
    publish_arch=$1
    publish_family=${2:-}
    if [ "$publish_arch" != "$(linux_ci_architecture)" ]; then
        echo 'Image publication must run on the matching runner architecture' >&2
        return 1
    fi
    case "$publish_family" in
        ''|*[!a-z0-9-]*) echo 'An image toolchain family is required for publication' >&2; return 1 ;;
    esac
    publish_variants=$(linux_ci_variants "$publish_arch") || return 1
    publish_variants=$(printf '%s\n' "$publish_variants" | grep "^$publish_family/") || {
        echo 'Unsupported Linux CI image toolchain/architecture combination' >&2
        return 1
    }
    resolve_linux_ci_images || return 1
    case "$publish_arch" in
        x64) DOCKER_DEFAULT_PLATFORM=linux/amd64 ;;
        arm64) DOCKER_DEFAULT_PLATFORM=linux/arm64 ;;
        *) return 1 ;;
    esac
    export DOCKER_DEFAULT_PLATFORM
    for publish_variant in $publish_variants; do
        publish_base=${publish_variant%/*}
        publish_role=${publish_variant#*/}
        publish_image=$(linux_ci_image "$publish_base" "$publish_role" "$publish_arch") || return 1
        if docker manifest inspect "$publish_image" >/dev/null 2>&1; then
            echo "Already published: $publish_image"
            continue
        fi
        publish_sdk=$LINUX_DOTNET_SDK_VERSION
        case "$publish_base" in universal) publish_sdk=$LINUX_NATIVE_DOTNET_SDK_VERSION ;; esac
        publish_target=build-environment
        [ "$publish_role" != tester ] || publish_target=test-environment
        environment_image="dd-trace-dotnet/$publish_base-$publish_role-environment:$LINUX_CI_IMAGE_HASH-$publish_arch"
        docker build \
            --build-arg "DOTNETSDK_VERSION=$publish_sdk" \
            --tag "$environment_image" \
            --target "$publish_target" \
            --file "tracer/build/_build/docker/$publish_base.dockerfile" \
            tracer/build/_build || return 1
        if [ "$publish_role" = tester ]; then
            docker build --build-arg "TESTER_IMAGE=$environment_image" \
                --file .gitlab/linux-unit-tests.dockerfile --tag "$environment_image-ca" .gitlab || return 1
            environment_image="$environment_image-ca"
        fi
        docker build --build-arg "ENVIRONMENT_IMAGE=$environment_image" \
            --build-arg "DOTNETSDK_VERSION=$publish_sdk" --file .gitlab/linux-ci-environment.dockerfile \
            --tag "$publish_image" tracer/build/_build || return 1
        docker push "$publish_image" || return 1
    done
}
