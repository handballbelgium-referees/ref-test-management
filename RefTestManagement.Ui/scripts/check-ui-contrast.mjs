import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const uiRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const readSource = (path) => readFileSync(join(uiRoot, path), 'utf8');
const styles = readSource('src/styles.css');
const tailwindTheme = readSource('node_modules/tailwindcss/theme.css');
const indexHtml = readSource('src/index.html');
const home = readSource('src/app/home/home.html');
const detailTabs = readSource(
  'src/app/ref-tests/detail/components/ref-test-detail-tabs/ref-test-detail-tabs.html',
);
const results = readSource('src/app/ref-test/take/components/ref-test-results/ref-test-results.html');

function themeColor(token) {
  const declaration = new RegExp(
    `--color-${token}:\\s*(#[\\da-f]{3}(?:[\\da-f]{3})?)\\s*;`,
    'i',
  );
  const match = declaration.exec(styles) ?? declaration.exec(tailwindTheme);
  assert.ok(match, `Missing --color-${token} in the project or Tailwind theme.`);
  return match[1];
}

function relativeLuminance(color) {
  const match = /^#([\da-f]{3}|[\da-f]{6})$/i.exec(color);
  assert.ok(match, `Unsupported hex color: ${color}`);
  const hex =
    match[1].length === 3
      ? [...match[1]].map((channel) => channel.repeat(2)).join('')
      : match[1];
  const channels = hex.match(/.{2}/g);
  assert.ok(channels, `Unsupported hex color: ${color}`);
  const [red, green, blue] = channels.map((channel) => {
    const value = Number.parseInt(channel, 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function assertContrast(label, foreground, background) {
  const [lighter, darker] = [relativeLuminance(foreground), relativeLuminance(background)].sort(
    (left, right) => right - left,
  );
  const ratio = (lighter + 0.05) / (darker + 0.05);
  assert.ok(ratio >= 4.5, `${label}: ${ratio.toFixed(2)}:1 is below 4.5:1`);
  console.log(`${label}: ${ratio.toFixed(2)}:1`);
}

const white = themeColor('white');
const neutral50 = themeColor('neutral-50');
const neutral500 = themeColor('neutral-500');
const manageCardStart = home.indexOf('href="/ref-tests"');
const manageCardEnd = home.indexOf('</a>', manageCardStart);
assert.ok(manageCardStart >= 0 && manageCardEnd > manageCardStart, 'Manage card was not found.');
const manageCard = home.slice(manageCardStart, manageCardEnd);
const descriptionClass = /<p\b[^>]*class="([^"]*)"/.exec(manageCard)?.[1];
assert.ok(manageCard.includes('from-secondary-700'), 'Manage card must use secondary-700.');
assert.ok(manageCard.includes('to-secondary-800'), 'Manage card must use secondary-800.');
assert.ok(manageCard.includes('text-white'), 'Manage card text must use white.');
assert.ok(descriptionClass, 'Manage card description was not found.');
assert.ok(
  !descriptionClass.split(/\s+/).some((name) => name.startsWith('text-')),
  'Manage card description must inherit the high-contrast white text.',
);

assert.match(detailTabs, /^<div class="bg-white[^"]*">/, 'Detail tabs need a white background.');
const detailTabLinks = [...detailTabs.matchAll(/<a\b[^>]*class="([^"]*)"/g)];
assert.equal(detailTabLinks.length, 2, 'Both detail tabs must be present.');
for (const [, classes] of detailTabLinks) {
  assert.ok(classes.split(/\s+/).includes('text-neutral-500'), 'Both tabs must use neutral-500.');
}
assert.ok(detailTabs.includes('!text-primary-600'), 'Active detail tabs must use primary-600.');
assert.match(
  results,
  /class="[^"]*bg-white[^"]*"[\s\S]*?<span class="text-lg text-neutral-500">/,
  'Secondary result scores must use neutral-500 text on a white card.',
);

for (const [label, path] of [
  [
    'Timeline card',
    'src/app/ref-tests/detail/components/ref-test-detail-tab/components/timeline-card/timeline-card.html',
  ],
  [
    'Test info card',
    'src/app/ref-tests/detail/components/ref-test-detail-tab/components/test-info-card/test-info-card.html',
  ],
]) {
  const template = readSource(path);
  assert.match(template, /^<div class="[^"]*bg-neutral-50/, `${label} background was not found.`);
  assert.equal(
    (template.match(/<span class="text-neutral-500">-<\/span>/g) ?? []).length,
    2,
    `${label} placeholders must use neutral-500.`,
  );
}

assertContrast('Home gradient start', white, themeColor('secondary-700'));
assertContrast('Home gradient end', white, themeColor('secondary-800'));
assertContrast('Detail tabs', neutral500, white);
assertContrast('Active detail tab', themeColor('primary-600'), white);
assertContrast('Result secondary scores', neutral500, white);
assertContrast('Detail placeholders', neutral500, neutral50);

const viewportContent = /<meta\s+name="viewport"\s+content="([^"]*)"/i.exec(indexHtml)?.[1];
assert.ok(viewportContent, 'Viewport metadata was not found.');
assert.match(viewportContent, /width=device-width/i, 'Viewport width must remain responsive.');
assert.match(viewportContent, /initial-scale\s*=\s*1(?:[;,]|$)/i, 'Viewport must start at scale 1.');
assert.doesNotMatch(
  viewportContent,
  /user-scalable\s*=\s*no|(?:maximum|minimum)-scale\s*=/i,
  'Viewport metadata must allow user zoom.',
);
