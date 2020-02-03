# .NET Core Docker Sample
This sample demonstrates how to build container images for .NET Core apps that use the Datadog .NET Tracer.

The instructions assume that you have cloned this repo, have [Docker](https://www.docker.com/products/docker) installed, and have a command prompt open within the customer-samples/ConsoleApp directory within the repo.

## Build a .NET Core image
You can build and run a .NET Core-based container image using the following instructions, specifying the desire dockerfile with the `-f` argument:

```console
docker build -f Debian.dockerfile -t consoleapp .
docker run --rm -it consoleapp
```
