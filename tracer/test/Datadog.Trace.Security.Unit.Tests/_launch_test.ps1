$n = 0
While($n -lt 100)
{
#nuke runmanagedunittests --verbosity normal -filter Waf
dotnet test --no-restore --configuration release
$n++
}