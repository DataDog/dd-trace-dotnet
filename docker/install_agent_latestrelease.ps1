$ProgressPreference = 'SilentlyContinue'

echo "Installing agent"
if ($env:os -eq "Windows_NT") 
{
    choco install -ia="APIKEY=""$env:DD_API_KEY""" datadog-agent
} 
else 
{
    DD_AGENT_MAJOR_VERSION=7 DD_API_KEY=$env:DD_API_KEY DD_SITE="datadoghq.com" bash -c "$(curl -L https://s3.amazonaws.com/dd-agent/scripts/install_script.sh)"
}

echo "Done."
