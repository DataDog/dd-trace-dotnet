FROM alpine:3.11

RUN apk update && apk upgrade

RUN apk add --no-cache --update bash alpine-sdk ruby ruby-dev ruby-etc

RUN gem install fpm
