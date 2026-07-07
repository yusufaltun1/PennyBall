#!/usr/bin/env bash
# PennyBall — Git LFS + Unity Smart Merge kurulum scripti
# Kullanım: ./scripts/setup-unity-git.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_VERSION_FILE="${PROJECT_ROOT}/ProjectSettings/ProjectVersion.txt"

UNITY_VERSION=""
if [[ -f "${PROJECT_VERSION_FILE}" ]]; then
  UNITY_VERSION="$(grep 'm_EditorVersion:' "${PROJECT_VERSION_FILE}" | awk '{print $2}')"
fi

UNITY_YAML_MERGE=""

find_unity_yaml_merge() {
  local version="${UNITY_VERSION}"
  local candidates=()

  if [[ -n "${version}" ]]; then
    candidates+=(
      "/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/Helpers/UnityYAMLMerge"
      "/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/Tools/UnityYAMLMerge"
    )
  fi

  candidates+=(
    "/Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge"
    "/Applications/Unity/Unity.app/Contents/Tools/UnityYAMLMerge"
  )

  local path
  for path in "${candidates[@]}"; do
    if [[ -x "$path" ]]; then
      UNITY_YAML_MERGE="$path"
      return 0
    fi
  done

  local found
  found="$(find /Applications/Unity/Hub/Editor -name UnityYAMLMerge -type f 2>/dev/null | head -n 1 || true)"
  if [[ -n "$found" && -x "$found" ]]; then
    UNITY_YAML_MERGE="$found"
    return 0
  fi

  return 1
}

echo "=== PennyBall Git + Unity kurulumu ==="

if ! command -v git >/dev/null; then
  echo "Hata: git bulunamadı."
  exit 1
fi

if ! command -v git-lfs >/dev/null; then
  echo ""
  echo "Git LFS yüklü değil. Önce kur:"
  echo "  brew install git-lfs"
  echo ""
  echo "Kurduktan sonra bu scripti tekrar çalıştır."
  exit 1
fi

echo "→ Git LFS etkinleştiriliyor..."
git lfs install

if find_unity_yaml_merge; then
  echo "→ Unity Smart Merge bulundu: $UNITY_YAML_MERGE"
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver "\"$UNITY_YAML_MERGE\" merge -p %O %B %A %A"
  git config merge.unityyamlmerge.recursive binary
else
  echo ""
  echo "Uyarı: UnityYAMLMerge bulunamadı."
  echo "Unity Hub'dan proje sürümü (${UNITY_VERSION:-bilinmiyor}) kurulu olduğundan emin ol."
  echo "Unity 6 yolu örneği:"
  echo "  .../Unity.app/Contents/Helpers/UnityYAMLMerge"
  echo ""
fi

echo ""
echo "Kontrol:"
git lfs version
git config --get merge.unityyamlmerge.driver || true
echo ""
echo "Tamam. Sıradaki adımlar:"
echo "  1) Unity → Edit → Project Settings"
echo "     • Version Control → Mode: Visible Meta Files"
echo "     • Editor → Asset Serialization: Force Text"
echo "     (Arama kutusuna 'meta' veya 'serialization' yaz)"
echo "  2) .gitattributes commit et"
echo "  3) git lfs track (zaten .gitattributes'ta tanımlı)"
echo "  4) Yeni branch aç, küçük PR gönder"
