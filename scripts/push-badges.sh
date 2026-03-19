#!/usr/bin/env bash
set -e

if git ls-remote --exit-code --heads origin badges; then
  git fetch origin badges
  git worktree add /tmp/badges-branch origin/badges
else
  git worktree add --orphan -b badges /tmp/badges-branch
fi

cp badges/*.svg /tmp/badges-branch/

cd /tmp/badges-branch
git add .
if ! git diff --cached --quiet; then
  git commit -m "chore: update badges [skip ci]"
fi
git push origin HEAD:refs/heads/badges
