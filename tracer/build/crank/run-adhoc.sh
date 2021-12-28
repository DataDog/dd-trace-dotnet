scenario=$1
profile=$2
appsec=$3

sha="$(git rev-parse HEAD)"
echo "sha=$sha"
echo "scenario=$scenario"
echo "appsec=$appsec"

shopt -s nocasematch
if [[  $appsec =~ (y|yes|t|true|1) ]]; then
    config_file=Security.Samples.AspNetCoreSimpleController.yml
else
    config_file=Samples.AspNetCoreSimpleController.yml
fi


repo="https://github.com/DataDog/dd-trace-dotnet"
commit_sha=$sha

echo "Using repo=$repo commit=$commit_sha"

repository="--application.source.repository $repo"
commit="--application.source.branchOrCommit #$commit_sha"

crank --config $config_file --scenario $scenario --profile $profile --json $scenario.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=$scenario --property profile=$profile --property arch=x64 --variable commit_hash=$commit_sha
