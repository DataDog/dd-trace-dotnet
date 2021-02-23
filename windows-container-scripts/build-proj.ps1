
$proj_file=Get-ChildItem -Path C:\site\*.csproj

nuget restore $proj_file -PackagesDirectory packages
msbuild $proj_file
