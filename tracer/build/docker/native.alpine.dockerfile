FROM alpine:3.11

RUN apk update && apk upgrade

RUN apk add --no-cache --update clang cmake git bash make alpine-sdk
