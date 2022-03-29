# Agent configuration
The `docker-compose.yml` file automatically starts the Datadog agent container image when running the applications.
- `DD_API_KEY` (required)
- `DD_ENV` (optional)

# Running samples
From this directory, run the docker-compose service for the corresponding container you want to test.

## Alpine Linux example
```
docker-compose build alpine
docker-compose run alpine
```

## Linux example
```
docker-compose build linux
docker-compose run linux
```

## Linux ARM64 example
```
docker-compose build linux-arm64
docker-compose run linux-arm64
```

## Windows .NET Core (x64) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker-compose build windows-netcore-x64
docker-compose run windows-netcore-x64
```

## Windows .NET Core (x86) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker-compose build windows-netcore-x86
docker-compose run windows-netcore-x86
```

## .NET Framework (x64) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker-compose build windows-netframework-x64
docker-compose run windows-netframework-x64
```

## .NET Framework (x86) example
Note: This requires running on Windows and switching Docker Desktop to Windows containers

```
docker-compose build windows-netframework-x86
docker-compose run windows-netframework-x86
```