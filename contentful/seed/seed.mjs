import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createClient } from "contentful-management";
import "dotenv/config";

const __dirname = dirname(fileURLToPath(import.meta.url));
const fixture = JSON.parse(readFileSync(join(__dirname, "fixtures/entries.json"), "utf8"));

const spaceId = process.env.CONTENTFUL_SPACE_ID;
const accessToken = process.env.CONTENTFUL_MANAGEMENT_TOKEN;
const environmentId = process.env.CONTENTFUL_ENVIRONMENT ?? "master";
const LOCALE = "en-US";

if (!spaceId || !accessToken) {
  console.error("Missing CONTENTFUL_SPACE_ID or CONTENTFUL_MANAGEMENT_TOKEN.");
  process.exit(1);
}

// contentful-management v12 defaults to the flat "plain" API; this script uses the
// chainable getSpace()/getEnvironment() legacy shape, so opt into it explicitly.
const client = createClient({ accessToken }, { type: "legacy" });
const space = await client.getSpace(spaceId);
const env = await space.getEnvironment(environmentId);

// Localise flat fixture fields into the CMA's { fieldName: { locale: value } } shape.
const localise = (fields) =>
  Object.fromEntries(Object.entries(fields).map(([k, v]) => [k, { [LOCALE]: v }]));

// Generate a real, fetchable placeholder image URL keyed by asset ID.
const placeholderImageUrl = (assetId) => `https://picsum.photos/seed/${assetId}/1200/630`;

// Check if an error is a 404 NotFound vs. a real auth/network error.
const isNotFoundError = (err) => err?.name === "NotFound";

// Recursively strip Link fields that reference skipped IDs.
const stripSkippedLinks = (fields, skippedIds) => {
  const strip = (value) => {
    if (!value) return value;

    // Single Link: check if this is a Link to a skipped entry
    if (typeof value === "object" && value.sys?.type === "Link" && value.sys?.id) {
      return skippedIds.has(value.sys.id) ? null : value;
    }

    // Array: filter out Links to skipped entries, recursively strip nested objects
    if (Array.isArray(value)) {
      return value
        .map(strip)
        .filter(item => item !== null);
    }

    // Nested object: recursively process
    if (typeof value === "object" && !value.sys) {
      return Object.fromEntries(
        Object.entries(value)
          .map(([k, v]) => [k, strip(v)])
          .filter(([, v]) => v !== null && v !== undefined)
      );
    }

    return value;
  };

  return Object.fromEntries(
    Object.entries(fields)
      .map(([k, v]) => [k, strip(v)])
      .filter(([, v]) => v !== null && v !== undefined)
  );
};

const allEntries = [...(fixture.includes?.Entry ?? []), ...(fixture.items ?? [])];

// Identify entries to skip and collect them in a Set.
const skippedIds = new Set();
for (const entry of allEntries) {
  if (entry.sys.contentType.sys.id === "sectionFutureThing") {
    skippedIds.add(entry.sys.id);
  }
}

const failures = [];

// Asset seeding pass: create and process all assets first.
const assets = fixture.includes?.Asset ?? [];
for (const asset of assets) {
  try {
    // Check if asset already exists
    try {
      await env.getAsset(asset.sys.id);
      console.log(`asset exists ${asset.sys.id}`);
      continue;
    } catch (err) {
      if (!isNotFoundError(err)) throw err;
      // Asset doesn't exist, proceed to create
    }

    // Create asset with placeholder image URL
    const uploadUrl = placeholderImageUrl(asset.sys.id);
    await env.createAssetWithId(asset.sys.id, {
      fields: {
        title: { [LOCALE]: asset.fields.title },
        file: {
          [LOCALE]: {
            contentType: asset.fields.file.contentType,
            fileName: asset.fields.file.fileName,
            upload: uploadUrl
          }
        }
      }
    });
    console.log(`created asset ${asset.sys.id}`);

    // Process the asset (fetch and store the file)
    const createdAsset = await env.getAsset(asset.sys.id);
    await createdAsset.processForAllLocales();
    console.log(`processing asset ${asset.sys.id}`);

    // Poll until the file URL is populated (up to 10 attempts, ~1s each)
    let processed = false;
    for (let attempt = 0; attempt < 10; attempt++) {
      await new Promise(resolve => setTimeout(resolve, 1000));
      const polledAsset = await env.getAsset(asset.sys.id);
      if (polledAsset.fields?.file?.[LOCALE]?.url) {
        await polledAsset.publish();
        console.log(`published asset ${asset.sys.id}`);
        processed = true;
        break;
      }
    }
    if (!processed) {
      console.warn(`asset ${asset.sys.id} did not finish processing within timeout, skipping publish`);
    }
  } catch (err) {
    const msg = `asset ${asset.sys.id}: ${err.message}`;
    console.error(msg);
    failures.push(msg);
  }
}

// Entry creation pass: create unpublished entries, stripping skipped links.
for (const entry of allEntries) {
  try {
    const contentTypeId = entry.sys.contentType.sys.id;
    if (skippedIds.has(entry.sys.id)) {
      console.log(`skip ${entry.sys.id} (intentionally unknown type, offline fixture only)`);
      continue;
    }

    // Check if entry already exists
    try {
      await env.getEntry(entry.sys.id);
      console.log(`exists ${entry.sys.id}`);
      continue;
    } catch (err) {
      if (!isNotFoundError(err)) throw err;
      // Entry doesn't exist, proceed to create
    }

    // Strip any links to skipped entries before creating
    const strippedFields = stripSkippedLinks(entry.fields, skippedIds);
    await env.createEntryWithId(contentTypeId, entry.sys.id, { fields: localise(strippedFields) });
    console.log(`created ${entry.sys.id}`);
  } catch (err) {
    const msg = `entry ${entry.sys.id}: ${err.message}`;
    console.error(msg);
    failures.push(msg);
  }
}

// Entry publish pass: publish all non-skipped entries.
for (const entry of allEntries) {
  try {
    if (skippedIds.has(entry.sys.id)) continue;
    const e = await env.getEntry(entry.sys.id);
    if (!e.isPublished()) {
      await e.publish();
      console.log(`published ${entry.sys.id}`);
    }
  } catch (err) {
    const msg = `publish ${entry.sys.id}: ${err.message}`;
    console.error(msg);
    failures.push(msg);
  }
}

// Summary
if (failures.length > 0) {
  console.log(`\n${failures.length} failure(s):`, failures);
  process.exit(1);
}

console.log("\nSeed complete.");
