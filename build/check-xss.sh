#!/usr/bin/env bash
# XSS guard (AU-04, T7, plan review P11). Fails on any HTML sink or banned API in the client source, and on any
# localize.term call whose first argument is not a string literal. Run from the repository root.
set -euo pipefail

SRC="src/Umbraco.Community.ArchivedUsers/Client/src"
status=0

if grep -rnE 'unsafeHTML|unsafeSVG|unsafeStatic|innerHTML|outerHTML|insertAdjacentHTML|DOMParser|createContextualFragment|document\.write|setHTMLUnsafe|srcdoc|\beval\(|new Function\(|UMB_CONFIRM_MODAL|htmlString|htmlMessage' "$SRC"; then
  echo "check-xss: banned HTML sink or API found (AU-04)" >&2
  status=1
fi

if grep -rnE "\.term\(\s*[^'\"[:space:]]" "$SRC"; then
  echo "check-xss: localize.term must take a string literal key (P11)" >&2
  status=1
fi

exit $status
