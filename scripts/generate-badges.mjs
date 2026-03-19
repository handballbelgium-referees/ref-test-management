#!/usr/bin/env node
import { makeBadge } from 'badge-maker';
import { mkdirSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const badgesDir = join(__dirname, '../badges');

const version = process.argv[2];
if (!version) {
  console.error('Usage: node generate-badges.mjs <version>');
  process.exit(1);
}

const isPreRelease = version.includes('-');

mkdirSync(badgesDir, { recursive: true });

if (isPreRelease) {
  const svg = makeBadge({
    label: 'pre-release',
    message: `v${version}`,
    color: 'orange',
    style: 'flat',
  });
  writeFileSync(join(badgesDir, 'pre-release.svg'), svg);
  console.log(`Generated pre-release badge: v${version}`);
} else {
  const releaseSvg = makeBadge({
    label: 'release',
    message: `v${version}`,
    color: '0075ca',
    style: 'flat',
  });
  writeFileSync(join(badgesDir, 'release.svg'), releaseSvg);
  console.log(`Generated release badge: v${version}`);

  const preReleaseSvg =
    '<svg xmlns="http://www.w3.org/2000/svg" width="0" height="0"/>';
  writeFileSync(join(badgesDir, 'pre-release.svg'), preReleaseSvg);
  console.log('Hide pre-release badge (no active pre-release)');
}
