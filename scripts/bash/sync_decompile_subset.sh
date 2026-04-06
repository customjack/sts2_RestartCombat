#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/_load_env.sh"

SOURCE_ROOT_DEFAULT="${PROJECT_ROOT}/../../decompile"
if [[ -n "${STS2_INSTALL_DIR:-}" && -d "${STS2_INSTALL_DIR}/modding/decompile" ]]; then
  SOURCE_ROOT_DEFAULT="${STS2_INSTALL_DIR}/modding/decompile"
fi
SOURCE_ROOT="${SOURCE_ROOT:-${SOURCE_ROOT_DEFAULT}}"
TARGET_ROOT="${PROJECT_ROOT}/decompile"

FILES=(
  "MegaCrit.Sts2.Core.Entities.Multiplayer/NetScreenTypeExtensions.cs"
  "MegaCrit.Sts2.Core.Modding/ModManager.cs"
  "MegaCrit.Sts2.Core.Modding/ModManifest.cs"
  "MegaCrit.Sts2.Core.Modding/ModSettings.cs"
  "MegaCrit.Sts2.Core.Nodes.CommonUi/NModalContainer.cs"
  "MegaCrit.Sts2.Core.Nodes.CommonUi/NSettingsScreenPopup.cs"
  "MegaCrit.Sts2.Core.Nodes.Multiplayer/NGenericPopup.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen/NModdingScreen.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen/NModMenuRow.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen/NModInfoContainer.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.Settings/NSettingsScreen.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.Settings/NSettingsButton.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.Settings/NOpenModdingScreenButton.cs"
  "MegaCrit.Sts2.Core.Nodes.Screens.Settings/NSettingsPanel.cs"
)

for rel in "${FILES[@]}"; do
  src="${SOURCE_ROOT}/${rel}"
  dst="${TARGET_ROOT}/${rel}"
  mkdir -p "$(dirname "${dst}")"
  cp -f "${src}" "${dst}"
  echo "Synced: ${rel}"
done
