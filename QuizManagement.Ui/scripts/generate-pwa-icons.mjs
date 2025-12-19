#!/usr/bin/env node

/**
 * PWA icon generator from SVG using sharp
 * White / neutral background only
 *
 * Requires:
 *   npm install --save-dev sharp
 */

import { existsSync, mkdirSync, readFileSync, statSync } from 'fs';
import { join } from 'path';
import sharp from 'sharp';
import { fileURLToPath } from 'url';

const __dirname = fileURLToPath(new URL('.', import.meta.url));

/* -------------------------------------------------------------------------- */
/* Configuration                                                              */
/* -------------------------------------------------------------------------- */

const svgPath = join(__dirname, '../public/URBH-KBHB-logo.svg');
const outputDir = join(__dirname, '../public/icons');

const ICONS = [
  // Android / PWA
  { size: 192, name: 'android-chrome-192x192.png' },
  { size: 256, name: 'android-chrome-256x256.png' },
  { size: 512, name: 'android-chrome-512x512.png' },

  // Maskable (extra padding, still white)
  { size: 192, name: 'maskable-192x192.png', maskable: true },
  { size: 512, name: 'maskable-512x512.png', maskable: true },

  // Apple
  { size: 60, name: 'apple-touch-icon-60x60.png' },
  { size: 76, name: 'apple-touch-icon-76x76.png' },
  { size: 120, name: 'apple-touch-icon-120x120.png' },
  { size: 152, name: 'apple-touch-icon-152x152.png' },
  { size: 180, name: 'apple-touch-icon.png' },

  // Windows
  { size: 150, name: 'mstile-150x150.png' },

  // Favicons
  { size: 16, name: 'favicon-16x16.png', favicon: true },
  { size: 32, name: 'favicon-32x32.png', favicon: true },
];

/* -------------------------------------------------------------------------- */
/* Setup                                                                       */
/* -------------------------------------------------------------------------- */

if (!existsSync(svgPath)) {
  console.error(`❌ SVG not found: ${svgPath}`);
  process.exit(1);
}

if (!existsSync(outputDir)) {
  mkdirSync(outputDir, { recursive: true });
}

const svgBuffer = readFileSync(svgPath);

console.log('🎨 Generating white-background PWA icons...\n');

/* -------------------------------------------------------------------------- */
/* Main                                                                        */
/* -------------------------------------------------------------------------- */

async function generateIcons() {
  for (const icon of ICONS) {
    const { size, name, maskable = false, favicon = false } = icon;

    const padding = maskable ? 0.25 : favicon ? 0.05 : 0.1;
    const logoSize = Math.round(size * (1 - padding * 2));
    const outputPath = join(outputDir, name);

    let svgContent = svgBuffer.toString();

    // If favicon, remove <line> and <text>
    if (favicon) {
      // Remove text
      svgContent = svgContent.replace(/<text[\s\S]*?<\/text>/g, '');

      // Remove horizontal black lines
      svgContent = svgContent.replace(/<line[^>]*\/>/g, '');
      svgContent = svgContent.replace(/<path[^>]*class="cls-32"[^>]*\/?>/g, '');

      // Keep the full viewBox (no horizontal crop, optionally shrink bottom)
      svgContent = svgContent.replace(
        /viewBox="([\d.-]+) ([\d.-]+) ([\d.-]+) ([\d.-]+)"/,
        (_, x, y, width, height) => {
          // optional bottom shrink; set to 0 if no crop at all
          const cropBottom = 0;
          const newHeight = parseFloat(height) - cropBottom;
          return `viewBox="${x} ${y} ${width} ${newHeight}"`;
        }
      );
    }

    // Wrap SVG only if not favicon
    const wrappedSvg = favicon
      ? Buffer.from(svgContent)
      : wrapSvgWithWhiteBackground(Buffer.from(svgContent), logoSize);

    // Resize the logo
    const resizedLogo = await sharp(wrappedSvg, { density: 144 })
      .resize(logoSize, logoSize, { fit: 'contain' })
      .png()
      .toBuffer();

    // Create final PNG with background
    const sharpInstance = sharp({
      create: {
        width: size,
        height: size,
        channels: 4,
        background: favicon ? 'transparent' : '#ffffff', // transparant for favicons, white otherwise
      },
    }).composite([{ input: resizedLogo, gravity: 'center' }]);

    await sharpInstance.png({ compressionLevel: 9 }).toFile(outputPath);

    const { size: fileSize } = statSync(outputPath);
    console.log(`✅ ${name.padEnd(32)} ${size}x${size}  ${(fileSize / 1024).toFixed(1)} KB`);
  }

  console.log('\n✨ Icon generation complete');
}

function wrapSvgWithWhiteBackground(svgBuffer, size) {
  return Buffer.from(`
    <svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}"
         xmlns="http://www.w3.org/2000/svg">
      <rect width="100%" height="100%" fill="#ffffff"/>
      <image
        href="data:image/svg+xml;base64,${svgBuffer.toString('base64')}"
        x="0"
        y="0"
        width="100%"
        height="100%"
        preserveAspectRatio="xMidYMid meet"/>
    </svg>
  `);
}

generateIcons().catch((err) => {
  console.error('❌ Icon generation failed:', err);
  process.exit(1);
});
