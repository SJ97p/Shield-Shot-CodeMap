# Publication Review

## Link Policy

The documentation assumes the public repository will be:

- Repository: https://github.com/sj97p/Shield-Shot-CodeMap
- Pages: https://sj97p.github.io/Shield-Shot-CodeMap/

If the repository name changes, update `README.md`, `index.html`, and `app.js`.

## Sensitive Information Scan

Initial scan found no backend keys, API tokens, passwords, or service secrets in the copied source snapshot.

Expected hits:

- `using Fusion;` and Photon Fusion terminology appear in PvP source files.
- `Key` appears as ordinary C# dictionary/member terminology, not as a credential.

## Source Snapshot Scope

The snapshot currently includes 25 C# files focused on:

- `GameplayCore/Field`
- `GameplayCore/Weapon/Projectile`
- `GameplayCore/Weapon/Aim`
- `Network/PvP`

Large files intentionally included because they are central to the portfolio narrative:

- `ArenaTerrainPainter.cs`
- `ElementFieldGrid.cs`
- `NetworkWeaponManager.cs`
- `ProjectileBase.cs`
- `NetworkProjectileFireHandler.cs`

## Before Publishing

- Review copied source for debug logs that should be cleaned or explained.
- Confirm Photon Fusion source usage is acceptable for public portfolio snippets.
- Do not add Unity assets, prefabs, ScriptableObjects, backend keys, generated build output, or local logs.
- Add GIF/MP4 evidence later under `assets/evidence`.
