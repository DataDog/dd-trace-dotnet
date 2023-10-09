#!/bin/bash
set -eo pipefail

sha="$(git rev-parse HEAD)"
echo "sha=$sha"
echo "SYSTEM_PULLREQUEST_SOURCECOMMITID=$SYSTEM_PULLREQUEST_SOURCECOMMITID"
echo "BUILD_SOURCEVERSION=$BUILD_SOURCEVERSION"
echo "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI=$SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
echo "BUILD_REPOSITORY_URI=$BUILD_REPOSITORY_URI"

repo="$SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
commit_sha="$SYSTEM_PULLREQUEST_SOURCECOMMITID"

if [ -z "$repo" ]; then
    repo="$BUILD_REPOSITORY_URI"
fi

if [ -z "$commit_sha" ]; then
    commit_sha="$BUILD_SOURCEVERSION"
fi

echo "Using repo=$repo commit=$commit_sha"

repository="--application.source.repository $repo"
commit="--application.source.branchOrCommit #$commit_sha"

if [ "$1" = "linux" ]; then
    echo "Running Linux  x64 throughput tests"

    rm -f appsec_baseline.json
    rm -f appsec_noattack.json
    rm -f appsec_attack_noblocking.json
    rm -f appsec_attack_blocking.json
    rm -f appsec_iast_enabled_default.json
    rm -f appsec_iast_enabled_full.json
    rm -f appsec_iast_disabled_vulnerability.json
    rm -f appsec_iast_enabled_vulnerability.json

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_baseline --profile linux --json appsec_baseline.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_baseline.json"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_noattack --profile linux --json appsec_noattack.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_noattack --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_noattack.json"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_attack_noblocking --profile linux --json appsec_attack_noblocking.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_attack_noblocking --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_attack_noblocking.json"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_attack_blocking --profile linux --json appsec_attack_blocking.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_attack_blocking --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_attack_blocking.json"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_iast_enabled_default --profile linux --json appsec_iast_enabled_default.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_iast_enabled_default --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_iast_enabled_default.json"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_iast_enabled_full --profile linux --json appsec_iast_enabled_full.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_iast_enabled_full --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_iast_enabled_full.json"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_iast_disabled_vulnerability --profile linux --json appsec_iast_disabled_vulnerability.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_iast_disabled_vulnerability --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_iast_disabled_vulnerability.json"
	
    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario appsec_iast_enabled_vulnerability --profile linux --json appsec_iast_enabled_vulnerability.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_iast_enabled_vulnerability --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="appsec_iast_enabled_vulnerability.json"
else
    echo "Unknown argument $1"
    exit 1
fi