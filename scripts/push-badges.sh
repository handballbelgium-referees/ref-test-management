#!/usr/bin/env bash
set -e

VERSION=${1:?"Version argument is required"}

# Push badges to the current branch (main for pre-releases, release for stable releases).
TARGET_BRANCH=$(git rev-parse --abbrev-ref HEAD)

git add badges/*.png
if ! git diff --cached --quiet; then
  git commit -m "chore(release): update badges to v${VERSION} [skip ci]"
  git push "https://x-access-token:${GH_PAT}@github.com/${GITHUB_REPOSITORY}.git" HEAD:refs/heads/${TARGET_BRANCH}
  echo "Badges committed and pushed to ${TARGET_BRANCH}"
else
  echo "No badge changes to commit"
fi
