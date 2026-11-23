#!/bin/sh
# Shared image inventory and lifecycle. Source from the repository root.

linux_ci_architecture() {
    case "$(uname -m)" in
        x86_64) printf '%s\n' x64 ;;
        aarch64) printf '%s\n' arm64 ;;
        *) echo 'Unsupported Linux runner architecture' >&2; return 1 ;;
    esac
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

    # C# sources are deliberately excluded: changing NUKE code must not require
    # publishing environments. Its restore inputs are included to warm packages.
    inputs='global.json
.gitlab/linux-ci-images.sh
.gitlab/build-linux-image.sh
.gitlab/get-dotnet-sdk-version.sh
.gitlab/linux-ci-environment.dockerfile
.gitlab/linux-unit-tests.dockerfile
tracer/build/_build/.dockerignore
tracer/build/_build/docker/debian.dockerfile
tracer/build/_build/docker/alpine.dockerfile
tracer/build/_build/docker/centos7.dockerfile
tracer/build/_build/docker/universal.dockerfile'
    restore_inputs=$(find tracer/build/_build -maxdepth 1 -type f \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) | sort) || return 1
    checksums=$(
        for input in $inputs $restore_inputs; do
            sha256sum "$input" || exit 1
        done
    ) || return 1
    digest=$(printf '%s\n' "$checksums" "$LINUX_DOTNET_SDK_VERSION" "$LINUX_NATIVE_DOTNET_SDK_VERSION" | sha256sum) || return 1
    LINUX_CI_IMAGE_HASH=$(printf '%s' "$digest" | cut -c 1-12)

    printf 'LINUX_CI_IMAGE_BASE=%s\nLINUX_CI_IMAGE_HASH=%s\nLINUX_DOTNET_SDK_VERSION=%s\nLINUX_NATIVE_DOTNET_SDK_VERSION=%s\n' \
        "$LINUX_CI_IMAGE_BASE" "$LINUX_CI_IMAGE_HASH" "$LINUX_DOTNET_SDK_VERSION" "$LINUX_NATIVE_DOTNET_SDK_VERSION"
}

load_linux_ci_images() {
    # Downloaded from the resolver in this same shadow pipeline, never a prior run.
    set -a
    . .gitlab/generated/linux-ci-images.env
    set +a
}

linux_ci_image() {
    image_base=$1
    image_role=$2
    image_arch=${3:-$(linux_ci_architecture)}
    case "$image_base/$image_role/$image_arch" in
        debian/builder/x64|debian/builder/arm64|debian/tester/x64|debian/tester/arm64|\
        alpine/builder/x64|alpine/builder/arm64|alpine/tester/x64|alpine/tester/arm64|\
        centos7/builder/x64|universal/builder/x64|universal/builder/arm64) ;;
        *) echo 'Unsupported Linux CI image variant' >&2; return 1 ;;
    esac
    printf '%s:%s-%s-%s-%s\n' "${LINUX_CI_IMAGE_BASE:?Missing image metadata}" \
        "${LINUX_CI_IMAGE_HASH:?Missing image metadata}" "$image_base" "$image_role" "$image_arch"
}

pull_linux_ci_image() {
    pull_arch=$(linux_ci_architecture) || return 1
    pull_image=$(linux_ci_image "$1" "$2" "$pull_arch") || return 1
    if ! docker pull "$pull_image" >&2; then
        echo "Cannot pull Linux CI image: $pull_image" >&2
        echo "If it is missing, review the image changes and manually run 'build-linux-ci-images-$pull_arch' in the root pipeline, then retry this job." >&2
        echo 'This job never builds or publishes images. For authentication/network errors, resolve those before retrying; rebuilding may not be needed.' >&2
        return 1
    fi
    printf '%s\n' "$pull_image"
}

publish_linux_ci_images() {
    publish_arch=$1
    if [ "$publish_arch" != "$(linux_ci_architecture)" ]; then
        echo 'Image publication must run on the matching runner architecture' >&2
        return 1
    fi
    resolve_linux_ci_images || return 1
    case "$publish_arch" in
        x64) DOCKER_DEFAULT_PLATFORM=linux/amd64; publish_bases='debian alpine centos7 universal' ;;
        arm64) DOCKER_DEFAULT_PLATFORM=linux/arm64; publish_bases='debian alpine universal' ;;
        *) return 1 ;;
    esac
    export DOCKER_DEFAULT_PLATFORM
    for publish_base in $publish_bases; do
        publish_roles=builder
        case "$publish_base" in debian|alpine) publish_roles='builder tester' ;; esac
        for publish_role in $publish_roles; do
            publish_image=$(linux_ci_image "$publish_base" "$publish_role" "$publish_arch") || return 1
            if docker manifest inspect "$publish_image" >/dev/null 2>&1; then
                echo "Already published: $publish_image"
                continue
            fi
            publish_sdk=$LINUX_DOTNET_SDK_VERSION
            case "$publish_base" in centos7|universal) publish_sdk=$LINUX_NATIVE_DOTNET_SDK_VERSION ;; esac
            publish_target=build-environment
            [ "$publish_role" != tester ] || publish_target=test-environment
            environment_image="dd-trace-dotnet/$publish_base-$publish_role-environment:$LINUX_CI_IMAGE_HASH-$publish_arch"
            sh .gitlab/build-linux-image.sh "$publish_base" "$publish_sdk" "$environment_image" "$publish_target" || return 1
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
    done
}
