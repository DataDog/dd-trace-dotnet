FROM golang:1.18

# print versions
RUN go version && curl --version

COPY utils/build/docker/golang/install_ddtrace.sh binaries* /binaries/
COPY utils/build/docker/golang/app /app

WORKDIR /app

RUN /binaries/install_ddtrace.sh
ENV DD_TRACE_HEADER_TAGS='user-agent'

RUN go build -v -tags appsec -o weblog ./echo.go ./common.go ./grpc.go ./weblog_grpc.pb.go ./weblog.pb.go

RUN echo "#!/bin/bash\n./weblog" > app.sh
RUN chmod +x app.sh
CMD ["./app.sh"]

# Datadog setup
ENV DD_LOGGING_RATE=0
