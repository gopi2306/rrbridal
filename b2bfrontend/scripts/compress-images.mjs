/**
 * Converts JPGs in public/images to a single optimized WebP per photo.
 *
 * Workflow: drop new photos as .jpg → npm run compress:images
 * Writes .webp (resized when needed) and deletes the source .jpg.
 */
import sharp from "sharp";
import { readdir, readFile, unlink, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, "..", "public", "images");

/** @type {{ subdir: string; maxWidth: number; webpQuality: number }[]} */
const RULES = [
  { subdir: "hero", maxWidth: 1920, webpQuality: 80 },
  { subdir: "banners", maxWidth: 1920, webpQuality: 80 },
  { subdir: "dresses", maxWidth: 1200, webpQuality: 82 },
];

async function listJpegs(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    if (entry.isDirectory()) continue;
    if (/\.jpe?g$/i.test(entry.name)) files.push(path.join(dir, entry.name));
  }
  return files;
}

async function processFile(filePath, opts) {
  const input = await readFile(filePath);
  const beforeKb = input.length / 1024;
  const name = path.relative(ROOT, filePath);
  const webpPath = filePath.replace(/\.jpe?g$/i, ".webp");

  const meta = await sharp(input).metadata();
  let pipeline = sharp(input).rotate();
  if (meta.width && meta.width > opts.maxWidth) {
    pipeline = pipeline.resize({
      width: opts.maxWidth,
      withoutEnlargement: true,
    });
  }

  const webpBuffer = await pipeline
    .webp({ quality: opts.webpQuality })
    .toBuffer();

  await writeFile(webpPath, webpBuffer);
  await unlink(filePath);

  console.log(
    `${name}: → ${path.basename(webpPath)} ${(webpBuffer.length / 1024).toFixed(0)} KB` +
      (meta.width && meta.width > opts.maxWidth
        ? ` (resized from ${meta.width}px wide)`
        : "") +
      ` — removed source jpg (${beforeKb.toFixed(0)} KB)`,
  );
}

async function main() {
  let count = 0;

  for (const rule of RULES) {
    const dir = path.join(ROOT, rule.subdir);
    const files = await listJpegs(dir);
    for (const file of files) {
      await processFile(file, rule);
      count += 1;
    }
  }

  if (count === 0) {
    console.log("No JPGs found — folders already WebP-only.");
  } else {
    console.log(`\nDone — converted ${count} image(s) to WebP.`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
