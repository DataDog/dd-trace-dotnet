FROM apache/kafka:3.9.2 AS kafka-stage

FROM registry.ddbuild.io/images/benchmarking-platform-tools-ubuntu:latest

ARG CI_JOB_TOKEN

SHELL ["/bin/bash", "-c"]

COPY ./install-base-deps.sh ./install-base-deps.sh
RUN ./install-base-deps.sh

COPY ./install-datadog-agent.sh ./install-datadog-agent.sh
RUN ./install-datadog-agent.sh

COPY --from=kafka-stage /opt/kafka /usr/local/kafka

# Download and install the tracer
ENV TRACER_VERSION=3.15.0
RUN if [ "$(uname -m)" = "arm64" ] || [ "$(uname -m)" = "aarch64" ]; then \
        ARCH="arm64"; \
    else \
        ARCH="amd64"; \
    fi && \
    echo "Installing Datadog .NET tracer for architecture: $ARCH" && \
    wget https://github.com/DataDog/dd-trace-dotnet/releases/download/v${TRACER_VERSION}/datadog-dotnet-apm_${TRACER_VERSION}_${ARCH}.deb && \
    dpkg -i ./datadog-dotnet-apm_${TRACER_VERSION}_${ARCH}.deb && \
    /opt/datadog/createLogPath.sh