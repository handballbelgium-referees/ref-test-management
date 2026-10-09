#!/usr/bin/env node
// Create the ephemeral approval artifact consumed by .ai/hooks/guards.mjs.
// Run only after the trusted host/user approval gate has completed.
import { createHash, sign as signData } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const output = path.join(repositoryRoot, ".ai", "approvals", "active.json");
const privateKeyFile = process.env.AI_APPROVAL_PRIVATE_KEY_FILE
  ? path.resolve(process.env.AI_APPROVAL_PRIVATE_KEY_FILE)
  : "";

function canonicalize(value) {
  if (Array.isArray(value)) return `[${value.map(canonicalize).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalize(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function option(name) {
  const prefix = `--${name}=`;
  const value = process.argv.find((arg) => arg.startsWith(prefix));
  return value?.slice(prefix.length) ?? "";
}

const planId = option("plan-id");
const workPackageId = option("work-package-id");
const approvedBy = option("approved-by");
const files = option("files").split(",").map((file) => file.trim()).filter(Boolean);
if (!planId || !workPackageId || !approvedBy || files.length === 0) {
  console.error("Usage: node .ai/scripts/create-approval.mjs --plan-id=PLAN --work-package-id=WP-01 --approved-by=USER --files=a.cs,b.cs");
  process.exit(2);
}

const approvedFiles = [...new Set(files.map((file) => file.replaceAll("\\", "/").replace(/^\.\//, "")))].sort();
if (approvedFiles.some((file) => !file || file === ".." || file.startsWith("../") || path.isAbsolute(file))) {
  console.error("approved files must be repository-relative paths");
  process.exit(2);
}

if (!privateKeyFile) {
  console.error("AI_APPROVAL_PRIVATE_KEY_FILE must point to a host-only Ed25519 private key. Do not expose the private key to agents.");
  process.exit(2);
}

let privateKey;
try {
  privateKey = readFileSync(privateKeyFile, "utf8");
} catch (error) {
  console.error(`Cannot read approval private key: ${error.message}`);
  process.exit(2);
}

const approval = {
  planId,
  workPackageId,
  approvedFiles,
  approvedBy,
  approvedAt: new Date().toISOString(),
};
approval.approvalHash = createHash("sha256").update(canonicalize(approval)).digest("hex");
approval.approvalSignature = signData(null, Buffer.from(canonicalize(approval)), privateKey).toString("base64url");
mkdirSync(path.dirname(output), { recursive: true });
writeFileSync(output, `${JSON.stringify(approval, null, 2)}\n`, { mode: 0o600 });
console.log(`Created ephemeral approval for ${workPackageId} covering ${approvedFiles.length} file(s).`);
