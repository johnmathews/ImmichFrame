# johnmathews/ImmichFrame

This fork exists to run patches before upstream merges them. Every patch is also an upstream pull
request, and the fork shrinks as they land.

## Branches

- `main` follows `immichFrame/ImmichFrame` `main` and carries no patches.
- One branch per patch, based on upstream `main`, each opened as an upstream PR.
- `deploy` = upstream `main` + every open patch. A push to `deploy` builds
  `ghcr.io/johnmathews/immichframe` (tags `deploy` and `sha-<commit>`) with
  `.github/workflows/build-fork-image.yml`. That workflow and this file never go upstream.

## Patches on `deploy`

| Branch | What | Upstream |
|---|---|---|
| `fix/request-edited-previews` | Request edited previews from Immich | PR #736, issue #680 |
| `pr-734` (Pixolantis) | Select account and albums via URL parameters | PR #734 |
| `feat/album-switching` | Up/down arrow keys switch albums | not yet opened; stacked on #734 |
| `feat/admin-album-picker` | Choose albums by name in `/admin` | not yet opened |
| `fix/focus-on-start` | Ask for keyboard focus on start, so a TV remote's keys work without a click | not yet opened |

## Updating

1. `git fetch upstream`, then fast-forward `main` to `upstream/main`.
2. Rebase each patch branch onto `main`. Drop any patch that upstream has merged.
3. Rebuild `deploy` from `main` by merging the patch branches, then add back the commit with this
   file and the workflow. Push it; CI builds the image.
