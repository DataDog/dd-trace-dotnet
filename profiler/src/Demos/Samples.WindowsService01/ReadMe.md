This demo is a [Windows Service](https://docs.microsoft.com/en-us/dotnet/framework/windows-services/introduction-to-windows-service-applications).
It can be run under F5 only as a trivial pair of start/stop handler invocations. To run this properly it needs to be installed as a Windows Service.
In addition, to demo profiling, the required environment variables need to be set via the registry.

# How to install

(Examples here assume that your product enlistment root is `c:\00\Code\GitHubDD\DD-DotNet\`. Adjust the paths for your system accordingly.)

1. Start the Developer Command Prompt for VS _in admin mode_.

```
**********************************************************************
** Visual Studio 2019 Developer Command Prompt v16.8.2
** Copyright (c) 2020 Microsoft Corporation
**********************************************************************

C:\Windows\System32>
```

2. Go to the binary directory for the demo.

```
C:\Windows\System32>cd C:\00\Code\GitHubDD\DD-DotNet\

C:\00\Code\GitHubDD\DD-DotNet>cd _build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\

C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01>
```

3. Run the Install Util.

```
C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01>installutil Samples.WindowsService01.exe
```

You should see a bunch of diagnostic output and eventual success messages. 
_If you encounter errors, make sure you have started the command prompt in Admin Mode._

```
Microsoft (R) .NET Framework Installation utility Version 4.8.3752.0
Copyright (C) Microsoft Corporation.  All rights reserved.


Running a transacted installation.

Beginning the Install phase of the installation.
See the contents of the log file for the C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.exe assembly's progress.
The file is located at C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.InstallLog.
Installing assembly 'C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.exe'.
Affected parameters are:
   logtoconsole =
   logfile = C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.InstallLog
   assemblypath = C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.exe
Installing service Datadog_Demos_WindowsService01...
Service Datadog_Demos_WindowsService01 has been successfully installed.
Creating EventLog source Datadog_Demos_WindowsService01 in log Application...

The Install phase completed successfully, and the Commit phase is beginning.
See the contents of the log file for the C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.exe assembly's progress.
The file is located at C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.InstallLog.
Committing assembly 'C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.exe'.
Affected parameters are:
   logtoconsole =
   logfile = C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.InstallLog
   assemblypath = C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01\Samples.WindowsService01.exe

The Commit phase completed successfully.

The transacted install has completed.
```

All set. Remember to un-install your service later, by passing the `/u` switch to the `installutil` tool. (Do not forget to run as Admin for that too.)

See also:
https://docs.microsoft.com/en-us/dotnet/framework/windows-services/how-to-install-and-uninstall-services#uninstall-using-installutilexe-utility.

# How to run

You can control Windows Services via [SC](https://ss64.com/nt/sc.html) command line tool. For example:

```
C:\00\Code\GitHubDD\DD-DotNet\_build\bin\Debug-AnyCPU\Demos\Samples.WindowsService01>sc GetDisplayName Datadog_Demos_WindowsService01
[SC] GetServiceDisplayName SUCCESS
Name = Datadog Profiler Demos: Windows Service 01
```

The standard Windows GUI tool is also very convenient.
To start it, press the Win+R keys to bring up Run dialog box.
There, type `services.msc` and press Enter. 

The Services GUI lists the services by their display names.
* The display name of this demo is "_Datadog Profiler Demos: Windows Service 01_".
* The identity-moniker is "`Datadog_Demos_WindowsService01`". 

Find the service in the list and use the buttons at the top of the window to Start/Stop/Pause/Restart.

# How to profile

You will need to set the environment variables in the Windows Registry and then restart the service.

1. To start the Registry Editor, press the Win+R keys to bring up Run dialog box.
There, type `regedit` and press Enter. 

2. Navigate to the key `Computer\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\<Service-Id-Moniker>`. In this case:

```
Computer\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Datadog_Demos_WindowsService01
```

3. There create a new Multi-String Value with the name `Environment` and the type `REG_MULTI_SZ`.

4. Populate the Value you just created with the required environment settings:

```
COR_ENABLE_PROFILING=1
COR_PROFILER={BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}
COR_PROFILER_PATH_64=c:\00\Code\GitHubDD\DD-DotNet\profiler\_build\DDProf-Deploy\win-x64\Datadog.Profiler.Native.dll
COR_PROFILER_PATH_32=c:\00\Code\GitHubDD\DD-DotNet\profiler\_build\DDProf-Deploy\win-x86\Datadog.Profiler.Native.dll

CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}
CORECLR_PROFILER_PATH_64=c:\00\Code\GitHubDD\DD-DotNet\profiler\_build\DDProf-Deploy\win-x64\Datadog.Profiler.Native.dll
CORECLR_PROFILER_PATH_32=c:\00\Code\GitHubDD\DD-DotNet\profiler\_build\DDProf-Deploy\win-x86\Datadog.Profiler.Native.dll

DD_API_KEY=<!YOUR API KEY!>
```

Make sure to use a valid API key. If you do not, the profiler will run, but no data will arrive at the backend.

The Registry Editor tool may complain about empty lines in the Multi-String Value. That is benign - it'll remove it for you.

Add any more of the supported environment configuration settings, as required.

5. Switch to the services UI.
Shut down the service.
Wait until the shutdown is complete.
Start the service again.
Wait until starting is complete (pausing/resuming will not be enough).

6. Profiler should attach and start working. Logs should be found in the default (or specified) log location. The default is `C:\ProgramData\Datadog-APM\logs\DotNet\`. 

For some additional info, see https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-framework/?tab=environmentvariables#installation-and-getting-started

Enjoy!