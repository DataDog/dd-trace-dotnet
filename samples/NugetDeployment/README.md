# Running samples

From this directory, here are the steps to building and running the sample applications. By default, the containers are sending traces to `host.docker.internal`. If the agent is elsewhere (e.g. a Datadog Agent container), set the environment variable `DD_AGENT_HOST` in the docker run command.

## Linux example
```
docker build -t netcoreapp -f Dockerfile .
docker run -it --rm --name myapp netcoreapp
```

## Linux ARM64 example
```
docker build -t netcoreapp -f Dockerfile.linuxarm64 .
docker run -it --rm --name myapp netcoreapp
```

## Alpine Linux example
```
docker build -t netcoreapp -f Dockerfile.alpine .
docker run -it --rm --name myapp netcoreapp
```

## Windows .NET Core (x64) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netcoreapp -f Dockerfile.windows-netcore64bit .
docker run -it --rm --name myapp netcoreapp
```

## Windows .NET Core (x86) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netcoreapp -f Dockerfile.windows-netcore32bit .
docker run -it --rm --name myapp netcoreapp
```

## .NET Framework (x64) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netframeworkapp -f Dockerfile.netframework64bit .
docker run -it --rm --name myapp netframeworkapp
```

## .NET Framework (x86) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker build -t netframeworkapp -f Dockerfile.netframework32bit .
docker run -it --rm --name myapp netframeworkapp
```

# Sending APM traffic from app container to Datadog Agent container
For more the latest docs, see https://docs.datadoghq.com/agent/docker/apm/?tab=standard#docker-network

## Linux
```
docker network create <NETWORK_NAME>

docker run -d --name datadog-agent
              --network <NETWORK_NAME> \
              -v /var/run/docker.sock:/var/run/docker.sock:ro \
              -v /proc/:/host/proc/:ro \
              -v /sys/fs/cgroup/:/host/sys/fs/cgroup:ro \
              -e DD_API_KEY=<DATADOG_API_KEY> \
              -e DD_APM_ENABLED=true \
              -e DD_APM_NON_LOCAL_TRAFFIC=true \
              gcr.io/datadoghq/agent:7

docker run -it --rm \
               --name myapp \
               --network <NETWORK_NAME> \
               -e DD_AGENT_HOST=datadog-agent \
               <APP_CONTAINER>
```

## Windows (using Powershell)
```
docker network create --driver nat <NETWORK_NAME>

docker run -d --name datadog-agent `
              --network <NETWORK_NAME> `
              -e DD_API_KEY=<DATADOG_API_KEY> `
              -e DD_APM_ENABLED=true `
              -e DD_APM_NON_LOCAL_TRAFFIC=true `
              gcr.io/datadoghq/agent:7

docker run -it --rm `
               --name myapp `
               --network <NETWORK_NAME> `
               -e DD_AGENT_HOST=datadog-agent `
               <APP_CONTAINER>
```

## Additional environment variables
- `DD_ENV`
- `DD_HOSTNAME`