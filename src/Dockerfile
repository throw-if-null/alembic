FROM alpine:latest

RUN apk update && apk add --no-cache docker-cli curl jq

COPY entrypoint.sh .

ADD VERSION .

RUN chmod +x /entrypoint.sh

ENTRYPOINT ["./entrypoint.sh"]