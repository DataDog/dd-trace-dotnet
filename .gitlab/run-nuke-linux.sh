#!/bin/sh

run_nuke() {
    image="$1"
    shift

    sh .gitlab/run-linux-container.sh \
        --cap-add=SYS_PTRACE \
        --env NugetPackageDirectory=/project/packages \
        "$image" \
        sh .gitlab/run-nuke-linux-container.sh "$@"
}
