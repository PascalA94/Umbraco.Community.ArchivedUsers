#!/usr/bin/env bash
# Packs one nupkg per Umbraco line into <outdir> (default ./artifacts), each with the client bundle.
# LINES (space separated) picks the lines; the default is every line in eng/lines.txt.
# PACK_VERSION, when set, is the whole package version (a 19 tag, for example 19.0.0-rc.1) and must start with the line;
# otherwise the version comes from Directory.Build.props. Line 19 needs the .NET 11 SDK first on PATH.
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
pack_version="${PACK_VERSION:-}"

for line in $lines; do
  case "$line" in 17|18|19) ;; *) echo "unsupported line $line" >&2; exit 1;; esac
  version_arg=()
  if [ -n "$pack_version" ]; then
    case "$pack_version" in "$line".*) ;; *) echo "PACK_VERSION $pack_version does not belong to line $line" >&2; exit 1;; esac
    version_arg=("-p:Version=$pack_version")
  fi
  version="$(dotnet msbuild "$proj" "-p:UmbracoMajor=$line" ${version_arg[@]+"${version_arg[@]}"} -getProperty:Version | tr -d '[:space:]')"
  case "$version" in ''|*-) echo "invalid package version '$version' for line $line" >&2; exit 1;; esac
  node -e '
    const fs = require("fs"); const [f, v] = process.argv.slice(1);
    const j = JSON.parse(fs.readFileSync(f, "utf8")); j.version = v;
    fs.writeFileSync(f, JSON.stringify(j, null, 2) + "\n");' "$manifest" "$version"
  dotnet restore "$proj" "-p:UmbracoMajor=$line" --locked-mode -v q
  dotnet build "$proj" -c Release --no-restore "-p:UmbracoMajor=$line" ${version_arg[@]+"${version_arg[@]}"} -v q
  dotnet pack "$proj" -c Release --no-build "-p:UmbracoMajor=$line" ${version_arg[@]+"${version_arg[@]}"} -o "$out" -v q
  cp "$backup" "$manifest"
  echo "packed $line: $version"
done
