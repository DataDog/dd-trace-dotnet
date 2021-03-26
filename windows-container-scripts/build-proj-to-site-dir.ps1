
$project_directory=$args[0]

$proj_file=Get-ChildItem -Path C:\sln\$project_directory\*.csproj

nuget restore C:\sln\Datadog.Trace.sln
msbuild C:\sln\Datadog.Trace.proj /t:BuildCsharp /p:Configuration=Release

nuget restore $proj_file -PackagesDirectory C:\sln\packages
msbuild $proj_file

Move-Item -Path C:\sln\$project_directory -Destination C:\site
