#!/usr/bin/env node
/** Validates English locale JSON files parse and contain only string values. */
import { readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const roots = [
  join(repoRoot, "server/Theexonet.Api/html/locales/en"),
  join(repoRoot, "server/Theexonet.Status/wwwroot/locales/en"),
];

let failed = false;

for (const dir of roots) {
  for (const file of readdirSync(dir)) {
    if (!file.endsWith(".json")) {
      continue;
    }
    const path = join(dir, file);
    try {
      const data = JSON.parse(readFileSync(path, "utf8"));
      for (const [key, value] of Object.entries(data)) {
        if (typeof value !== "string") {
          console.error(`${path}: key "${key}" must be a string`);
          failed = true;
        }
      }
      console.log(`OK ${path} (${Object.keys(data).length} keys)`);
    } catch (error) {
      console.error(`${path}: ${error.message}`);
      failed = true;
    }
  }
}

if (failed) {
  process.exit(1);
}
