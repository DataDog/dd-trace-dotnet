# Development

## Table of contents

- [Windows](#windows)
  * [Build Windows Profiler](#build-windows-profiler)
    + [Setup and minimum requirements](#setup-and-minimum-requirements)
    + [Building in Visual Studio](#building-windows-in-visual-studio)
  * [Build Linux Profiler](#build-linux-profiler)
    + [Setup and minimum requirements](#setup-and-minimum-requirements-1)
    + [Building from the command line](#building-from-the-command-line)
- [Linux](#linux)
  * [Minimun requirements](#minimun-requirements)
  * [Building from the command line](#building-from-the-command-line-1)

<hr />

## Windows

### Build Windows Profiler

#### Setup and minimum requirements
- [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/)
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - .NET Core cross-platform development
  - Individual components
    - .NET Framework 4.7 targeting pack
    - C++ for Linux Development
    - Windows 10 SDK (10.0.18362)

#### Building in Visual Studio

In order to build the profiler library, you need first to build the `Managed Loader` :

Open the solution `dd-trace-dotnet\shared\src\managed-lib\ManagedLoader\ManagedLoader.sln` and build the `Datadog.AutoInstrumentation.ManagedLoader` project.

Now, you can open the solution `dd-trace-dotnet\profiler\src\ProfilerEngine\Datadog.AutoInstrumentation.Profiler.sln` and build the projects `Datadog.Profiler.Managed` (C#) and `Datadog.Profiler.Native.Windows` (C++).

### Build Linux Profiler

#### Setup and minimum requirements

To build C++ project
- [Setup WSL on your machine](https://docs.microsoft.com/en-us/windows/wsl/install-win10)
- Install a Linux Distribution (In this document, we assume you have installed [Ubuntu](https://ubuntu.com/wsl))

To build C# projects
- [Install .NET 5.0 SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux)

Make sure that your [Linux distribution is the default one](https://docs.microsoft.com/en-us/windows/wsl/wsl-config#set-a-default-distribution) (the one you get when you run `wsl.exe`. This is required by Visual Studio). In our case, Ubuntu will be the default one.

- Install required compiler (clang >= 9), tools and libraries

```bash
sudo apt update
sudo apt upgrade
# recommended clang >= 9
sudo apt install clang git libssl-dev autoconf libtool liblzma-dev
```

- Build and install CMake (>= 3.14)
```bash
curl -OL https://github.com/Kitware/CMake/releases/download/v3.21.1/cmake-3.21.1.tar.gz 
tar zxf cmake-3.21.1.tar.gz
cd cmake-3.21.1

./bootstrap
make 
sudo make install 
```


#### Building from the command line
1. Build C# projects

- Managed loader
```bash
cd dd-trace-dotnet/shared/src/managed-lib/ManagedLoader/Datadog.AutoInstrumentation.ManagedLoader
dotnet build Datadog.AutoInstrumentation.ManagedLoader.csproj
```

- Managed Profiler
```bash
cd dd-trace-dotnet/profiler/src/ProfilerEngine/Datadog.Profiler.Managed
dotnet build Datadog.Profiler.Managed.csproj
```

2. Build C++ projects and run the unit tests
```bash
CXX=clang++ CC=clang cmake -S dd-trace-dotnet -B _build
cd _build
make
ctest
```

## Linux

### Minimun requirements

To build C# projects
- [Install .NET 5.0 SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux)

To build C++ projects
- Clang >= 9.0 (recommended)
- CMake >= 3.14
- Libtool
- liblzma
- libssl-dev
- autoconf 
- git

### Building from the command line
1. Build C# projects

- Managed loader
```bash
cd dd-trace-dotnet/shared/src/managed-lib/ManagedLoader/Datadog.AutoInstrumentation.ManagedLoader
dotnet build Datadog.AutoInstrumentation.ManagedLoader.csproj
```

- Managed Profiler
```bash
cd dd-trace-dotnet/profiler/src/ProfilerEngine/Datadog.Profiler.Managed
dotnet build Datadog.Profiler.Managed.csproj
```

2. Build C++ projects and run the unit tests
```bash
CXX=clang++ CC=clang cmake -S dd-trace-dotnet -B _build
cd _build
make
ctest
```
