#!/bin/sh
# Local verification of deploy/Caddyfile + deploy/maintenance.html. Run from this directory.
set -u
D=../../deploy
docker network create bm-net >/dev/null
echo "== caddy validate (production Caddyfile)"
docker run --rm -e BINGO_DOMAIN=example.test -v "$PWD/$D/Caddyfile:/etc/caddy/Caddyfile:ro" -v "$PWD/$D/maintenance.html:/srv/maintenance/maintenance.html:ro" caddy:2-alpine caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile; echo "exit=$?"
echo "== caddy adapt (head)"
docker run --rm -e BINGO_DOMAIN=example.test -v "$PWD/$D/Caddyfile:/etc/caddy/Caddyfile:ro" caddy:2-alpine caddy adapt --config /etc/caddy/Caddyfile --adapter caddyfile --pretty | grep -c . 
docker run -d --name bm-stub --network bm-net --network-alias web -v "$PWD/stub.Caddyfile:/etc/caddy/Caddyfile:ro" caddy:2-alpine >/dev/null
docker run -d --name bm-caddy --network bm-net -p 127.0.0.1:18099:18099 -e BINGO_DOMAIN=localhost:18099 -v "$PWD/$D/Caddyfile:/etc/caddy/Caddyfile:ro" -v "$PWD/$D/maintenance.html:/srv/maintenance/maintenance.html:ro" caddy:2-alpine >/dev/null
sleep 4
U=https://localhost:18099
echo "== (a) upstream up: GET /  (expect 200 APP-RESPONSE 200, no Retry-After)"
curl -ksi $U/ | grep -iE "^HTTP|^retry-after|APP-RESPONSE"
echo "== (a) upstream up, app returns 503: GET /app503 (expect 503 APP-RESPONSE 503, no Retry-After, no maintenance page)"
curl -ksi $U/app503 | grep -iE "^HTTP|^retry-after|APP-RESPONSE|Siden"
echo "== /health/ready (expect 404 from Caddy)"
curl -ksi $U/health/ready | grep -iE "^HTTP"
echo "== stop upstream"
docker stop bm-stub >/dev/null
echo "== (b) no upstream: GET / (expect 503 + Retry-After: 60 + maintenance page)"
curl -ksi $U/ | grep -iE "^HTTP|^retry-after|^cache-control|^content-type|<h1|http-equiv"
echo "== (b) no upstream: GET /health/live (expect 503 maintenance, not 200)"
curl -ksi $U/health/live | grep -iE "^HTTP|^retry-after"
echo "== (b) no upstream: /health/ready still 404"
curl -ksi $U/health/ready | grep -iE "^HTTP"
echo "== (b) no upstream: POST / (expect 503 maintenance)"
curl -ksi -X POST $U/ | grep -iE "^HTTP|^retry-after"
echo "== www redirect"
curl -ksi --resolve www.localhost:18099:127.0.0.1 https://www.localhost:18099/x?y=1 | grep -iE "^HTTP|^location"
echo "== restart upstream: recovers (expect 200)"
docker start bm-stub >/dev/null; sleep 2
curl -ksi $U/ | grep -iE "^HTTP|APP-RESPONSE"
docker rm -f bm-caddy bm-stub >/dev/null; docker network rm bm-net >/dev/null
