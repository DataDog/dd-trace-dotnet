# Setup

## IDE

We recommend using [Rider](https://www.jetbrains.com/rider/download/#section=mac) for MacOS development

## Setup on MacOS

- Install [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet)
- Install cmake `brew install cmake`
- Ensure you can build the tracer `./tracer/build.sh BuildTracerHome`

### Running integration tests

- Start docker
- Start needed dependencies from `./docker-compose.yml`, for example for RabbitMQ: `docker-compose up rabbitmq_osx_arm64`
- Compile the project `./tracer/build.sh BuildTracerHome`
- Run a specific test. For example for RabbitMQ Data Streams tests: `./tracer/build.sh BuildAndRunOsxIntegrationTests -SampleName Samples.DataStreams.RabbitMQ -Filter RabbitMQTests -framework net8.0`
