scenario=$1
profile=$2
appsec=$3

echo "scenario=$scenario"
echo "appsec=$appsec"

shopt -s nocasematch
if [[  $appsec =~ (y|yes|t|true|1) ]]; then
    config_file=Security.Samples.AspNetCoreSimpleController.yml
else
    config_file=Samples.AspNetCoreSimpleController.yml
fi


repo="https://github.com/DataDog/dd-trace-dotnet"

echo "Using repo=$repo"

repository="--application.source.repository $repo"

crank --config $config_file --scenario $scenario --profile $profile --json $scenario.json $repository --property name=AspNetCoreSimpleController --property scenario=$scenario --property profile=$profile --property arch=x64
