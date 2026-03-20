#!/usr/bin/env node
import { makeBadge } from 'badge-maker';
import { mkdirSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import sharp from 'sharp';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const badgesDir = join(__dirname, '../badges');

const version = process.argv[2];
if (!version) {
  console.error('Usage: node generate-badges.mjs <version>');
  process.exit(1);
}

// 1x1 transparent PNG — renders as invisible, no broken image icon
const TRANSPARENT_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQI12NgAAIABQAABjkB6QAAAABJRU5ErkJggg==',
  'base64',
);

async function svgToPng(svg) {
  return sharp(Buffer.from(svg), { density: 1200 }).png().toBuffer();
}

const isPreRelease = version.includes('-');

mkdirSync(badgesDir, { recursive: true });

if (isPreRelease) {
  const png = await svgToPng(
    makeBadge({
      label: 'pre-release',
      message: `v${version}`,
      color: 'orange',
      style: 'flat',
    }),
  );
  writeFileSync(join(badgesDir, 'pre-release.png'), png);
  console.log(`Generated pre-release badge: v${version}`);
} else {
  const png = await svgToPng(
    makeBadge({
      label: 'release',
      message: `v${version}`,
      color: '0075ca',
      style: 'flat',
    }),
  );
  writeFileSync(join(badgesDir, 'release.png'), png);
  console.log(`Generated release badge: v${version}`);

  writeFileSync(join(badgesDir, 'pre-release.png'), TRANSPARENT_PNG);
  console.log('Hid pre-release badge (no active pre-release)');
}
