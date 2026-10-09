#!/usr/bin/env node
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const script = fileURLToPath(import.meta.url);
const defaultRoot = path.resolve(path.dirname(script), "../..");
const providers = ["claude", "codex", "github-copilot"];
const agents = ["implementer", "auditor", "reviewer"];

function keys(value, expected, location) {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`${location}: expected an object`);
  }
  for (const key of expected) {
    if (!Object.hasOwn(value, key)) throw new Error(`${location}: missing ${key}`);
  }
  for (const key of Object.keys(value)) {
    if (!expected.includes(key)) throw new Error(`${location}: unknown key ${key}`);
  }
}

function model(value, location) {
  if (value !== null && (typeof value !== "string" || value.trim() !== value ||
      !/^[a-zA-Z0-9][a-zA-Z0-9._:/-]*$/.test(value))) {
    throw new Error(`${location}: expected null or a nonempty model ID (letters, digits, . _ : / -)`);
  }
}

/** Validate the whole shared config; live model availability remains a host concern. */
export function loadModelConfig(root = defaultRoot) {
  const file = path.join(root, ".ai", "models.json");
  let config;
  try {
    config = JSON.parse(readFileSync(file, "utf8"));
  } catch (error) {
    if (error instanceof SyntaxError) throw new Error(".ai/models.json: invalid JSON", { cause: error });
    throw error;
  }
  keys(config, providers, ".ai/models.json");
  for (const provider of providers) {
    const entry = config[provider];
    keys(entry, ["default", "agents"], provider);
    model(entry.default, `${provider}.default`);
    keys(entry.agents, agents, `${provider}.agents`);
    for (const agent of agents) model(entry.agents[agent], `${provider}.agents.${agent}`);
  }
  return config;
}

/** An explicit override, including null for host inheritance, beats repository defaults. */
export function resolveModel(root, provider, agent, { override } = {}) {
  if (!providers.includes(provider)) throw new Error(`unknown provider: ${provider}; choose ${providers.join(", ")}`);
  if (!agents.includes(agent)) throw new Error(`unknown agent: ${agent}; choose ${agents.join(", ")}`);
  const config = loadModelConfig(root);
  if (override !== undefined) model(override, "explicit override");
  return {
    provider,
    agent,
    model: override !== undefined ? override : config[provider].agents[agent] ?? config[provider].default,
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === script) {
  try {
    const args = process.argv.slice(2);
    if (args.length !== 2) {
      throw new Error("usage: node .ai/scripts/resolve-model.mjs <claude|codex|github-copilot> <implementer|auditor|reviewer>");
    }
    console.log(JSON.stringify(resolveModel(defaultRoot, args[0], args[1])));
  } catch (error) {
    console.error(`resolve-model: ${error.message}`);
    process.exitCode = 2;
  }
}
