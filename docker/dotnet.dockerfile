FROM microsoft/dotnet:2.1-sdk

ADD https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh /bin/wait-for-it
RUN chmod +x /bin/wait-for-it

RUN apt-get update && apt-get install -y colortail
