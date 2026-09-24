#!/usr/bin/env bash
# Git-installed packages are read-only in Unity, so every file and folder Unity imports needs a
# committed .meta file (otherwise Unity ignores it). Checks that, and flags orphaned .meta files.
#
# Usage: check-metas.sh [--fix]   (--fix creates missing folder/text/script metas with new GUIDs)
set -euo pipefail

PACKAGE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIX="${1:-}"
QTN_IMPORTER_GUID="272e0f14b4b64ca6893aadb634328654"   # Quantum SDK: QuantumQtnAssetImporter.cs
status=0

new_guid() { python3 -c 'import uuid; print(uuid.uuid4().hex)'; }

write_meta() {
  local path="$1" guid; guid="$(new_guid)"
  if [ -d "$path" ]; then
    printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid"
  else
    case "$path" in
      *.cs) printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" ;;
      *.qtn) printf 'fileFormatVersion: 2\nguid: %s\nScriptedImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 2\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n  script: {fileID: 11500000, guid: %s, type: 3}\n' "$guid" "$QTN_IMPORTER_GUID" ;;
      *.asmref) printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionReferenceImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" ;;
      *.asmdef) printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" ;;
      *.md|*.json|*.txt) printf 'fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" ;;
      *) return 1 ;;
    esac
  fi > "$path.meta"
}

# Everything Unity imports: skip the package root itself, dot-entries, *~ folders and .meta files.
while IFS= read -r -d '' path; do
  if [ ! -f "$path.meta" ]; then
    if [ "$FIX" = "--fix" ] && write_meta "$path"; then
      echo "created ${path#$PACKAGE_ROOT/}.meta"
    else
      echo "missing meta: ${path#$PACKAGE_ROOT/}"; status=1
    fi
  fi
done < <(find "$PACKAGE_ROOT" -mindepth 1 \( -name '.*' -o -name '*~' \) -prune -o ! -name '*.meta' -print0)

while IFS= read -r -d '' meta; do
  [ -e "${meta%.meta}" ] || { echo "orphaned meta: ${meta#$PACKAGE_ROOT/}"; status=1; }
done < <(find "$PACKAGE_ROOT" -mindepth 1 \( -name '.*' -o -name '*~' \) -prune -o -name '*.meta' -print0)

# GUIDs must be unique.
dups="$(find "$PACKAGE_ROOT" \( -name '.*' -o -name '*~' \) -prune -o -name '*.meta' -print0 | xargs -0 -r grep -h '^guid:' | sort | uniq -d)"
[ -z "$dups" ] || { echo "duplicate GUIDs: $dups"; status=1; }

[ $status -eq 0 ] && echo "Metas: OK"
exit $status
