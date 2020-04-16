# build this from one tools directory: docker build . -f MockTraceAgent.Cli.dockerfile

# https://hub.docker.com/_/microsoft-dotnet-core
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY ./MockTraceAgent.Cli/*.csproj ./MockTraceAgent.Cli/
COPY ./MockTraceAgent/*.csproj ./MockTraceAgent/
RUN dotnet restore MockTraceAgent.Cli

# copy everything else and build app
COPY ./MockTraceAgent.Cli ./MockTraceAgent.Cli
COPY ./MockTraceAgent ./MockTraceAgent
RUN dotnet publish -c release -o /app --no-restore MockTraceAgent.Cli

# final stage/image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=build /app ./

ENTRYPOINT ["dotnet", "MockTraceAgent.Cli.dll"]