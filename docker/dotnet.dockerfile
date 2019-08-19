FROM microsoft/dotnet:2.1-sdk-ubuntu

RUN apk add bash libc6-compat

ADD https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh /bin/wait-for-it
RUN chmod +x /bin/wait-for-it
