# Version control & rollback (Unity project)

This project’s Git repository lives in **`nexus-game/`** (same folder as `Assets/` and `ProjectSettings/`).

## Before you change anything risky

```bash
cd nexus-game
git status
git add -A
git commit -m "checkpoint: before risky change"
```

## Roll back the last commit (keep files in working tree)

```bash
git reset --soft HEAD~1
```

## Roll back and discard the last commit’s changes

```bash
git reset --hard HEAD~1
```

## Go back to a specific commit

```bash
git log --oneline
git checkout <commit-hash>
```

To create a branch at that point:

```bash
git checkout -b recovery/<short-name> <commit-hash>
```

## If something is badly broken

```bash
git reflog
git checkout HEAD@{1}
```

---

**Note:** The parent folder `Nexus Ops Mobile App` is not the Git root; use **`nexus-game`** for commits and history.
