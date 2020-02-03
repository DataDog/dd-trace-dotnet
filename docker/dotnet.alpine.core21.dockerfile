FROM mcr.microsoft.com/dotnet/core/sdk:2.1-alpine3.9

RUN apk update && apk upgrade && apk add --no-cache --update bash libc6-compat gcompat

ADD https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh /bin/wait-for-it
RUN chmod +x /bin/wait-for-it
