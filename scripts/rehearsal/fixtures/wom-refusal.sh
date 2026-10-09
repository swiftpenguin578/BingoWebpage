#!/bin/sh
# Wise Old Man refusal fixture (one connection per invocation, run by busybox nc -e).
# Reads the request line and headers only, discards them without logging, and
# always answers 503 with an empty body. It never invents provider data.
cr=$(printf '\r')
while IFS= read -r line; do
    case "$line" in
        "" | "$cr") break ;;
    esac
done
printf 'HTTP/1.1 503 Service Unavailable\r\nContent-Type: text/plain\r\nContent-Length: 0\r\nConnection: close\r\n\r\n'
