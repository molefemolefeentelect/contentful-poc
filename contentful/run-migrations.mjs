import { readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { runMigration } from "contentful-migration";
import "dotenv/config";

const __dirname = dirname(fileURLToPath(import.meta.url));
const dir = join(__dirname, "migrations");

const spaceId = process.env.CONTENTFUL_SPACE_ID;
const accessToken = process.env.CONTENTFUL_MANAGEMENT_TOKEN;
const environmentId = process.env.CONTENTFUL_ENVIRONMENT ?? "master";

if (!spaceId || !accessToken) {
  console.error("Missing CONTENTFUL_SPACE_ID or CONTENTFUL_MANAGEMENT_TOKEN. Copy .env.example to .env first.");
  process.exit(1);
}

const files = readdirSync(dir).filter((f) => f.endsWith(".cjs")).sort();

for (const file of files) {
  console.log(`\n=== Running ${file} ===`);
  await runMigration({
    filePath: join(dir, file),
    spaceId,
    accessToken,
    environmentId,
    yes: true,
  });
}
console.log("\nAll migrations applied.");
