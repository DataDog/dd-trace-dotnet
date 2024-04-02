FROM python:3.10.5-windowsservercore-ltsc2022

WORKDIR /

# Pin to older test agent versions to try to avoid breakages in the future
RUN pip install --no-cache-dir "ddapm-test-agent==1.16.0" "ddsketch==3.0.1" "ddsketch[serialization]==3.0.1"

ENTRYPOINT [ "ddapm-test-agent", "--port=8126" ]