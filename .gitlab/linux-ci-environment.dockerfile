ARG ENVIRONMENT_IMAGE
FROM ${ENVIRONMENT_IMAGE}

# Cache NUKE's dependencies, not its compiled code or restore state. Jobs compile
# the candidate checkout and regenerate their own project.assets.json.
ARG DOTNETSDK_VERSION
COPY *.csproj *.props *.targets /nuke-dependencies/
RUN printf '{"sdk":{"version":"%s","rollForward":"disable"}}\n' "$DOTNETSDK_VERSION" > /nuke-dependencies/global.json \
    && cd /nuke-dependencies \
    && dotnet restore _build.csproj \
    && rm -rf /nuke-dependencies

WORKDIR /project
