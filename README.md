# RestartCombat

Adds a **Restart Combat** button to the in-run pause/settings menu.

**Features:**
- Restart the current combat encounter from the pause menu
- In multiplayer, the host can trigger a restart that resets all players

## Install

1. Download the latest release zip from the [Releases](../../releases) page.
2. Close Slay the Spire 2.
3. In Steam, right-click `Slay the Spire 2` -> `Properties` -> `Installed Files` -> `Browse`.
4. Create a `mods` folder in the game directory if it does not exist.
5. Extract the zip and drag the `RestartCombat` folder into `mods`.
6. Confirm these files are present in `mods/RestartCombat`:
   - `RestartCombat.dll`
   - `RestartCombat.pck`
   - `RestartCombat.json`
7. Launch Slay the Spire 2. If prompted to enable mods, accept and relaunch.
8. In-game, go to `Settings` -> `General` -> `Mods` and enable `RestartCombat`.

## Usage

During combat, open the pause menu and click **Restart Combat**. In multiplayer, only the host sees the button and the restart applies to all players.

## Developer Notes

**Requirements:** .NET SDK, Godot 4 export templates, WSL or Linux shell.

**Setup:**
1. Copy `.env.example` to `.env`.
2. Set `STS2_INSTALL_DIR` to your game install path.

**Build and install:**
```bash
./scripts/bash/build_and_stage.sh
./scripts/bash/make_pck.sh
./scripts/bash/install_to_game.sh
```

## License

MIT — see [LICENSE](LICENSE).
