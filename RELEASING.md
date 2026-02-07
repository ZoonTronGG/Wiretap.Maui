# Releasing Wiretap.Maui

Step-by-step guide for pushing changes and creating releases when branch protection is enabled.

## 1. Commit Your Changes Locally

```bash
git add <files>
git commit -m "Your commit message"
```

## 2. Create a Feature Branch

You can't push directly to `main` (branch protection). Move your commit to a branch:

```bash
# Create branch from current state (includes your commit)
git branch fix/your-branch-name

# Reset local main back to remote
git reset --hard origin/main

# Switch to your branch
git checkout fix/your-branch-name
```

If you already committed on `main` and realize you can't push — this same sequence works retroactively.

## 3. Push and Open a PR

```bash
git push -u origin fix/your-branch-name

gh pr create --title "Short description" --body "Details of the change"
```

## 4. Merge with Admin Bypass

Since you're the sole maintainer and can't approve your own PR:

```bash
gh pr merge <PR-NUMBER> --merge --admin
```

## 5. Update Local Main

```bash
git checkout main
git pull origin main
```

## 6. Tag the Release

Follow [semver](https://semver.org/): `vMAJOR.MINOR.PATCH`

- **PATCH** (v1.0.1): bug fixes, small changes
- **MINOR** (v1.1.0): new features, backwards-compatible
- **MAJOR** (v2.0.0): breaking changes

```bash
git tag v1.0.1
git push origin v1.0.1
```

## 7. Create a GitHub Release

```bash
gh release create v1.0.1 --title "v1.0.1" --notes "Release notes here"
```

Or with multiline notes:

```bash
gh release create v1.0.1 --title "v1.0.1" --notes "$(cat <<'EOF'
### What changed
- Fixed something
- Added something
EOF
)"
```

## 8. Clean Up

Delete the merged feature branch:

```bash
git branch -d fix/your-branch-name
git push origin --delete fix/your-branch-name
```

## Quick Reference (All-in-One)

After committing on `main` and needing to release:

```bash
# Branch, reset, push
git branch fix/my-change
git reset --hard origin/main
git checkout fix/my-change
git push -u origin fix/my-change

# PR + merge
gh pr create --title "Description" --body "Details"
gh pr merge <N> --merge --admin

# Tag + release
git checkout main
git pull origin main
git tag v1.0.X
git push origin v1.0.X
gh release create v1.0.X --title "v1.0.X" --notes "What changed"

# Clean up
git branch -d fix/my-change
git push origin --delete fix/my-change
```
