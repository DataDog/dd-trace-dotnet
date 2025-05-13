# ASP.NET Docker Sample
This sample demonstrates how to build and deploy an ASP.NET application in IIS using a Windows container. This also demonstrates how to configure environment variables during container startup, which may be the earliest time that IPs and networking values are populated.

## Build and Run
To build and run the application, open a terminal in this directory and run the following commands:

```console
docker build -t iis .
docker run -it --rm -p 8080:80 iis
```

Note: The application build is done entirely inside the Windows container, which means pulling the `dotnet/framework/sdk` image before starting the build. If you already have a .NET Framework SDK installed, you may want to instead build the application locally and copy the published binaries into the runtime container.