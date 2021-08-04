# Running samples

From the innermost `Net5WithDockerDeploy` directory, here's how to build and run the applications:

## Linux example
```
docker build -t netcoreapp -f .\Dockerfile .
docker run -it --rm --name myapp netcoreapp
```

## Linux ARM64 example
```
docker build -t netcoreapp -f .\Dockerfile.linuxarm64 .
docker run -it --rm --name myapp netcoreapp
```

## Alpine Linux example
```
docker build -t netcoreapp -f .\Dockerfile.alpine .
docker run -it --rm --name myapp netcoreapp
```

## Windows .NET Core (x64) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netcoreapp -f .\Dockerfile .
docker run -it --rm --name myapp netcoreapp
```

## Windows .NET Core (x86) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netcoreapp -f .\Dockerfile.netcore32bit .
docker run -it --rm --name myapp netcoreapp
```

## .NET Framework (x64) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netframeworkapp -f .\Dockerfile.netframework64bit .
docker run -it --rm --name myapp netframeworkapp
```

## .NET Framework (x86) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netframeworkapp -f .\Dockerfile.netframework32bit .
docker run -it --rm --name myapp netframeworkapp
```