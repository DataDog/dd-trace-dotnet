FROM python:3.10.5-windowsservercore-1809

WORKDIR /

RUN pip install --no-cache-dir ddapm-test-agent

ENTRYPOINT [ "ddapm-test-agent", "--port=8126" ]