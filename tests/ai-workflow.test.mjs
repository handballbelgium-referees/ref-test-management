import test from "node:test";
import assert from "node:assert/strict";
import { createHash, generateKeyPairSync, sign } from "node:crypto";
import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const guard = path.join(root, ".ai/hooks/guards.mjs");
const approval = path.join(root, ".ai/approvals/active.json");
const publicKeyFile = path.join(root, ".ai/approval-public-key.pem");
const approved = path.join(root, "tests/approved.txt");
const outside = path.join("/tmp", "ai-workflow-secret.txt");
const link = path.join(root, "tests/approved-link.txt");
let privateKeyFile;
let privateKey;

function canonicalize(value) {
  if (Array.isArray(value)) return `[${value.map(canonicalize).join(",")}]`;
  if (value && typeof value === "object") return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalize(value[key])}`).join(",")}}`;
  return JSON.stringify(value);
}

function createTestKey() {
  const pair = generateKeyPairSync("ed25519", { privateKeyEncoding: { type: "pkcs8", format: "pem" }, publicKeyEncoding: { type: "spki", format: "pem" } });
  privateKey = pair.privateKey;
  privateKeyFile = path.join(os.tmpdir(), `ai-approval-${process.pid}.pem`);
  writeFileSync(privateKeyFile, privateKey, { mode: 0o600 });
  writeFileSync(publicKeyFile, pair.publicKey, { mode: 0o644 });
}

function createApproval(files = ["tests/approved.txt"]) {
  execFileSync(process.execPath, [path.join(root, ".ai/scripts/create-approval.mjs"),
    "--plan-id=TEST-PLAN", "--work-package-id=WP-TEST", "--approved-by=test", `--files=${files.join(",")}`], {
    cwd: root,
    env: { ...process.env, AI_APPROVAL_PRIVATE_KEY_FILE: privateKeyFile },
  });
}

function hook(toolName, toolArgs, provider = "copilot") {
  const event = provider === "claude"
    ? { tool_name: toolName, tool_input: toolArgs, cwd: root }
    : { toolName, toolArgs, cwd: root };
  const result = spawnSync(process.execPath, [guard, "pre", `--provider=${provider}`], {
    cwd: root,
    input: JSON.stringify(event),
    encoding: "utf8",
    env: { ...process.env, AI_APPROVAL_PUBLIC_KEY_FILE: publicKeyFile },
  });
  return { ...result, decision: result.stdout ? JSON.parse(result.stdout.trim()) : null };
}

function resignApproval(value) {
  const copy = { ...value };
  delete copy.approvalHash;
  delete copy.approvalSignature;
  value.approvalHash = createHash("sha256").update(canonicalize(copy)).digest("hex");
  value.approvalSignature = sign(null, Buffer.from(canonicalize({ ...value, approvalHash: value.approvalHash })), privateKey).toString("base64url");
  writeFileSync(approval, `${JSON.stringify(value, null, 2)}\n`);
}

test.before(() => {
  mkdirSync(path.dirname(approval), { recursive: true });
  createTestKey();
});

test.beforeEach(() => {
  rmSync(approval, { force: true });
  rmSync(approved, { force: true });
  rmSync(outside, { force: true });
  rmSync(link, { force: true });
});

test.after(() => {
  rmSync(approval, { force: true });
  rmSync(publicKeyFile, { force: true });
  rmSync(approved, { force: true });
  rmSync(outside, { force: true });
  rmSync(link, { force: true });
  rmSync(privateKeyFile, { force: true });
});

test("denies writes without an approval artifact", () => {
  const result = hook("edit", { path: "tests/approved.txt", newText: "x" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /approval artifact/i);
});

test("allows an approved direct write for every provider", () => {
  for (const provider of ["copilot", "claude", "codex"]) {
    createApproval();
    const result = hook(provider === "codex" ? "apply_patch" : "edit", provider === "codex"
      ? { patch: "*** Begin Patch\n*** Update File: tests/approved.txt\n@@\n-x\n+y\n*** End Patch" }
      : { path: "tests/approved.txt", newText: "x" }, provider);
    assert.equal(result.status, 0, `${provider}: ${result.stderr}`);
    if (provider === "copilot") assert.equal(result.decision, null, result.stdout);
  }
});

test("denies an out-of-scope direct write", () => {
  createApproval();
  const result = hook("edit", { path: "tests/outside.txt", newText: "x" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /outside the approved/i);
});

test("denies an expired approval", () => {
  createApproval();
  const value = JSON.parse(readFileSync(approval, "utf8"));
  value.approvedAt = new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString();
  resignApproval(value);
  const result = hook("edit", { path: "tests/approved.txt", newText: "x" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /expired/i);
});

test("denies an approved symlink that escapes the repository", () => {
  writeFileSync(outside, "secret");
  symlinkSync(outside, link);
  createApproval(["tests/approved-link.txt"]);
  const result = hook("edit", { path: "tests/approved-link.txt", newText: "x" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /symlink|scope/i);
});

test("denies a tampered approval hash", () => {
  createApproval();
  const value = JSON.parse(readFileSync(approval, "utf8"));
  value.approvedFiles.push("tests/outside.txt");
  writeFileSync(approval, `${JSON.stringify(value, null, 2)}\n`);
  const result = hook("edit", { path: "tests/approved.txt", newText: "x" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /approvalHash/i);
});

test("denies a tampered approval signature even when the hash is recomputed", () => {
  createApproval();
  const value = JSON.parse(readFileSync(approval, "utf8"));
  value.approvedFiles.push("tests/outside.txt");
  resignApproval(value);
  const result = hook("edit", { path: "tests/approved.txt", newText: "x" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /approvalSignature|invalid/i);
});

test("denies shell redirection outside the approved scope", () => {
  createApproval();
  const result = hook("bash", { command: "printf x > tests/outside.txt" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /scope/i);
});

test("denies multiline apply_patch outside the approved scope for every provider", () => {
  for (const provider of ["copilot", "claude", "codex"]) {
    createApproval();
    const result = hook("bash", { command: "apply_patch <<'PATCH'\n*** Begin Patch\n*** Update File: tests/outside.txt\n@@\n+x\n*** End Patch\nPATCH" }, provider);
    if (provider === "copilot") {
      assert.equal(result.decision.permissionDecision, "deny", provider);
      assert.match(result.decision.permissionDecisionReason, /scope/i);
    } else {
      assert.equal(result.status, 2, provider);
      assert.match(result.stderr, /scope/i);
    }
  }
});

test("denies malformed apply_patch rather than guessing its target", () => {
  createApproval();
  const result = hook("bash", { command: "apply_patch <<'PATCH'\nnot a patch\nPATCH" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /unresolved|safely/i);
});

test("denies dynamic interpreter writes", () => {
  createApproval();
  const result = hook("bash", { command: "node -e \"require('fs').writeFileSync('tests/approved.txt','x')\"" });
  assert.equal(result.decision.permissionDecision, "deny");
  assert.match(result.decision.permissionDecisionReason, /dynamic|interpreter/i);
});

test("denies all agent pushes, including mirror and tags", () => {
  for (const command of ["git push origin feat/x", "git push --mirror", "git push --all", "git push --tags"]) {
    const result = hook("bash", { command });
    assert.equal(result.decision.permissionDecision, "deny", command);
  }
});

test("denies unknown or mutating Git aliases/commands", () => {
  for (const command of ["git p origin feat/x", "git config user.name test", "git branch new", "git remote set-url origin x"]) {
    const result = hook("bash", { command });
    assert.equal(result.decision.permissionDecision, "deny", command);
  }
});

test("denies backtick-derived PR titles", () => {
  const result = hook("bash", { command: "gh pr create --title '`git rev-parse --short HEAD`'" });
  assert.equal(result.decision.permissionDecision, "deny");
});

test("denies approval creation without a host-only signing key", () => {
  const result = spawnSync(process.execPath, [path.join(root, ".ai/scripts/create-approval.mjs"),
    "--plan-id=TEST-PLAN", "--work-package-id=WP-TEST", "--approved-by=test", "--files=tests/approved.txt"], {
    cwd: root, env: { ...process.env, AI_APPROVAL_PRIVATE_KEY_FILE: "" }, encoding: "utf8",
  });
  assert.equal(result.status, 2);
});

test("approval hash and signature are present and deterministic for the signed payload", () => {
  createApproval();
  const value = JSON.parse(readFileSync(approval, "utf8"));
  assert.match(value.approvalHash, /^[a-f0-9]{64}$/);
  assert.match(value.approvalSignature, /^[A-Za-z0-9_-]+$/);
  const copy = { ...value };
  delete copy.approvalHash;
  delete copy.approvalSignature;
  assert.equal(value.approvalHash, createHash("sha256").update(canonicalize(copy)).digest("hex"));
});
