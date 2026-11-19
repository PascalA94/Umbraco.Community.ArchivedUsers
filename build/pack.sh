#!/usr/bin/env bash
# Packs one nupkg per Umbraco line into <outdir> (default ./artifacts), each with the client bundle.
# LINES (space separated) picks the lines; the default is every line in eng/lines.txt.
# The client version files stay 0.0.0 in git; this stamps the line's package version into wwwroot/umbraco-package.json
# for the build and pack, then restores it, so the committed tree never changes.
# Usage: build/pack.sh [outdir]. Run from the repository root, after the client build and the tests.
set -euo pipefail
cd "$(dirname "$0")/.."
out="${1:-./artifacts}"
proj=src/Umbraco.Community.ArchivedUsers/Umbraco.Community.ArchivedUsers.csproj
manifest=src/Umbraco.Community.ArchivedUsers/wwwroot/umbraco-package.json
backup="$(mktemp)"
cp "$manifest" "$backup"
trap 'cp "$backup" "$manifest"; rm -f "$backup"' EXIT
mkdir -p "$out"

lines="${LINES:-$(tr '\n' ' ' < eng/lines.txt)}"

for line in $lines; do
  case "$line" in 17|18) ;; *) echo "unsupported line $line" >&2; exit 1;; esac
  version="$(dotnet msbuild "$proj" "-p:UmbracoMajor=$line" -getProperty:Version | tr -d '[:space:]')"
  case "$version" in ''|*-) echo "invalid package version '$version' for line $line" >&2; exit 1;; esac
  node -e '
    const fs = require("fs"); const [f, v] = process.argv.slice(1);
    const j = JSON.parse(fs.readFileSync(f, "utf8")); j.version = v;
    fs.writeFileSync(f, JSON.stringify(j, null, 2) + "\n");' "$manifest" "$version"
  dotnet restore "$proj" "-p:UmbracoMajor=$line" --locked-mode -v q
  dotnet build "$proj" -c Release --no-restore "-p:UmbracoMajor=$line" -v q
  dotnet pack "$proj" -c Release --no-build "-p:UmbracoMajor=$line" -o "$out" -v q
  cp "$backup" "$manifest"
  echo "packed $line: $version"
done
