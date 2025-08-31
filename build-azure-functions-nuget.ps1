# build tracer home and copy to bundle folder
#.\tracer\build.ps1 BuildTracerHome
#Copy-Item .\shared\bin\monitoring-home\* .\tracer\src\Datadog.Trace.Bundle\home\ -Recurse -Force
dotnet publish '.\tracer\src\Datadog.Trace' -c Release -o '.\tracer\src\Datadog.Trace.Bundle\home\net6.0' -f 'net6.0'
dotnet publish '.\tracer\src\Datadog.Trace' -c Release -o '.\tracer\src\Datadog.Trace.Bundle\home\net461' -f 'net461'

# build azure functions nuget and copy to temp folder
.\tracer\build.ps1 BuildAzureFunctionsNuget
Copy-Item '.\tracer\bin\artifacts\nuget\azure-functions\Datadog.AzureFunctions.*.nupkg' 'D:\temp\nuget' -Force

# remove nuget package from cache
C:\Users\Lucas.Pimentel\.local\bin\Remove-Package-From-Nuget-Cache.ps1 -PackageId 'Datadog.AzureFunctions'