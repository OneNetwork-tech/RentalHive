#!/usr/bin/env bash
# Creates .env for trying RentalHive on this computer, with freshly generated secrets.
#
#   ./setup-local.sh                                  # image tag "latest" (default branch)
#   ./setup-local.sh claude-charming-noether-lhy664   # the newest build of a branch
#
# Then: ./deploy.sh   and open http://localhost:8081 (API: http://localhost:8082)
# Needs Docker with Compose and openssl. For a real server, edit .env afterwards
# (see the comments in it): this sets plain-HTTP, test-friendly values.
set -euo pipefail
cd "$(dirname "$0")"

if [ -f .env ]; then
  echo "$(pwd)/.env already exists. Delete it (and run 'docker compose down -v' to wipe the data) to start over." >&2
  exit 1
fi

cp .env.example .env
chmod 600 .env

# Sets NAME=VALUE in .env (replaces an existing line, or appends one). Works with GNU and BSD sed.
setval() {
  if grep -qE "^$1=" .env; then
    sed -i.bak "s|^$1=.*|$1=$2|" .env && rm -f .env.bak
  else
    printf '%s=%s\n' "$1" "$2" >> .env
  fi
}

setval IMAGE_TAG "${1:-latest}"
setval POSTGRES_PASSWORD "$(openssl rand -hex 16)"
setval Security__PersonalNumberKey "$(openssl rand -base64 32)"
setval Jwt__Key "$(openssl rand -hex 48)"
setval Security__HttpsOnly false
setval App__PublicUrl http://localhost:8081
# No mail server: print notification emails in the log (docker compose logs web)
setval Email__LogBodies true

echo "Created $(pwd)/.env (image tag: ${1:-latest})."
echo "Next: ./deploy.sh   then open http://localhost:8081 (API: http://localhost:8082)"
echo "Make yourself a moderator after registering: docker compose exec web dotnet RentalHive.Web.dll make-admin you@example.com"
