#!/usr/bin/env node
import { existsSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const heavyFileLimit = 20 * 1024;
const forcedReadLimit = 60 * 1024;

function isRecord(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function errorMessage(error) {
  return error instanceof Error ? error.message : String(error);
}

async function readEvent() {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk.toString();
  }
  return JSON.parse(input);
}

function normalizeArguments(value) {
  if (isRecord(value)) {
    return value;
  }
  if (typeof value !== "string") {
    return value === undefined ? {} : { raw: value };
  }
  try {
    const parsed = JSON.parse(value);
    return isRecord(parsed) ? parsed : { raw: parsed };
  } catch {
    return { raw: value, command: value, patch: value };
  }
}

function getToolName(event) {
  return String(event.toolName ?? event.tool_name ?? "");
}

function getToolArguments(event) {
  return normalizeArguments(event.toolArgs ?? event.tool_input);
}

function getArgumentPath(args) {
  for (const key of ["path", "filePath", "file_path", "targetPath", "target_file"]) {
    if (typeof args[key] === "string" && args[key].length > 0) {
      return args[key];
    }
  }
  return typeof args.raw === "string" ? args.raw : "";
}

function resolveArgumentPath(event, args) {
  const filePath = getArgumentPath(args);
  if (!filePath) {
    return "";
  }
  const base = typeof event.cwd === "string" ? event.cwd : repositoryRoot;
  return path.resolve(base, filePath);
}

function getFileSize(filePath) {
  if (!filePath) {
    return null;
  }
  try {
    const stats = statSync(filePath);
    return stats.isFile() ? stats.size : null;
  } catch {
    return null;
  }
}

function normalizedPath(filePath) {
  return filePath.replace(/\\/g, "/");
}

function isHeavyFile(filePath) {
  const normalized = normalizedPath(filePath);
  return /(?:^|\/)package-lock\.json$/i.test(normalized)
    || /(?:^|\/)packages\.lock\.json$/i.test(normalized)
    || /(?:^|\/)graphql\/generated\.ts$/i.test(normalized)
    || /(?:^|\/)docs\/AUDIT[^/]*\.md$/i.test(normalized)
    || /(?:^|\/)public\/i18n\/[^/]+\.json$/i.test(normalized);
}

function hasViewRange(args) {
  const range = args.view_range ?? args.viewRange ?? args.range;
  if (Array.isArray(range)) {
    return range.length >= 2;
  }
  if (typeof range === "string") {
    return range.trim().length > 0;
  }
  return ["startLine", "endLine", "start_line", "end_line"]
    .some((key) => args[key] !== undefined && args[key] !== null);
}

function splitShellSegments(command, powershell) {
  const segments = [];
  let tokens = [];
  let token = "";
  let tokenStarted = false;
  let quote = "";

  const pushToken = () => {
    if (tokenStarted) {
      tokens.push(token);
      token = "";
      tokenStarted = false;
    }
  };
  const pushSegment = () => {
    pushToken();
    if (tokens.length > 0) {
      segments.push(tokens);
      tokens = [];
    }
  };

  for (let index = 0; index < command.length; index += 1) {
    const character = command[index];
    const next = command[index + 1];

    if (quote) {
      if (character === quote) {
        if (powershell && quote === "'" && next === "'") {
          token += "'";
          index += 1;
        } else {
          quote = "";
        }
      } else if (quote === '"' && (character === "\\" || (powershell && character === "`")) && next) {
        token += next;
        index += 1;
      } else {
        token += character;
      }
      tokenStarted = true;
      continue;
    }

    if (character === "'" || character === '"') {
      quote = character;
      tokenStarted = true;
    } else if (powershell && character === "`" && next) {
      token += next;
      tokenStarted = true;
      index += 1;
    } else if (character === "\\" && next && /[\s'"\\]/.test(next)) {
      token += next;
      tokenStarted = true;
      index += 1;
    } else if (character === ";" || character === "|" || character === "&"
      || character === "\n" || character === "\r") {
      pushSegment();
    } else if (/\s/.test(character)) {
      pushToken();
    } else {
      token += character;
      tokenStarted = true;
    }
  }

  pushSegment();
  return segments;
}

function shellCommandText(args) {
  const values = [args.command, args.script, args.cmd, args.shellCommand, args.raw];
  return values
    .filter((value) => typeof value === "string")
    .join("\n");
}

function shellPullRequestTitles(command, powershell) {
  const titles = [];
  for (const segment of splitShellSegments(command, powershell)) {
    for (let index = 0; index < segment.length - 2; index += 1) {
      const executable = path.basename(segment[index]).toLowerCase();
      if ((executable !== "gh" && executable !== "gh.exe")
        || segment[index + 1]?.toLowerCase() !== "pr"
        || !["create", "edit"].includes(segment[index + 2]?.toLowerCase())) {
        continue;
      }

      for (let option = index + 3; option < segment.length; option += 1) {
        const value = segment[option];
        if (value === "--title" || value === "-t") {
          const title = segment[option + 1];
          titles.push({
            title: title ?? "",
            dynamic: !title || /(?:\$\(|\$\{|(?:^|[^\\])\$[A-Za-z_]|%[A-Za-z_][A-Za-z0-9_]*%)/.test(title),
          });
          option += 1;
        } else if (value.startsWith("--title=") || value.startsWith("-t=")) {
          const title = value.slice(value.indexOf("=") + 1);
          titles.push({
            title,
            dynamic: !title || /(?:\$\(|\$\{|(?:^|[^\\])\$[A-Za-z_]|%[A-Za-z_][A-Za-z0-9_]*%)/.test(title),
          });
        }
      }
    }
  }
  return titles;
}

let commitlint;
async function getCommitlint() {
  if (commitlint) {
    return commitlint;
  }
  try {
    const [lintModule, loadModule] = await Promise.all([
      import("@commitlint/lint"),
      import("@commitlint/load"),
    ]);
    const lint = lintModule.default ?? lintModule.lint;
    const load = loadModule.default ?? loadModule.load;
    const config = await load({}, { cwd: repositoryRoot });
    commitlint = { lint, config };
    return commitlint;
  } catch (error) {
    process.stderr.write(`[copilot guard] commitlint unavailable; allowing title check to fail open: ${errorMessage(error)}\n`);
    return null;
  }
}

function configuredValues(config, ruleName) {
  const rule = config.rules?.[ruleName];
  return Array.isArray(rule) && Array.isArray(rule[2]) ? rule[2] : [];
}

async function lintTitle(title) {
  const loaded = await getCommitlint();
  if (!loaded) {
    return { valid: true, unavailable: true };
  }
  const result = await loaded.lint(title, loaded.config.rules, loaded.config);
  const errors = Array.isArray(result.errors)
    ? result.errors.map((error) => `${error.name}: ${error.message}`)
    : [];
  return { valid: result.valid !== false, errors, config: loaded.config };
}

function deny(reason) {
  process.stdout.write(`${JSON.stringify({
    permissionDecision: "deny",
    permissionDecisionReason: reason,
  })}\n`);
}

async function preToolUse(event, toolName, args) {
  const filePath = resolveArgumentPath(event, args);
  const size = getFileSize(filePath);
  const tool = toolName.toLowerCase();
  const forcedRead = args.forceReadLargeFiles === true || args.force_read_large_files === true;

  if (tool === "view" && forcedRead && size !== null && size > forcedReadLimit) {
    deny(`Do not force-read files larger than 60 KB. Use rg or jq, then read focused view_range chunks.`);
    return;
  }

  if (tool === "view" && size !== null && size > heavyFileLimit
    && isHeavyFile(filePath) && !hasViewRange(args)) {
    deny(`Avoid full-reading ${path.basename(filePath)} (${Math.ceil(size / 1024)} KB). Use rg or jq, then read a focused view_range; chunked reads are fine.`);
    return;
  }

  const candidates = [];
  if ((tool === "create_pull_request" || tool === "update_pull_request")
    && typeof args.title === "string") {
    candidates.push({ title: args.title, dynamic: false });
  }
  if (tool === "bash" || tool === "powershell") {
    candidates.push(...shellPullRequestTitles(shellCommandText(args), tool === "powershell"));
  }

  for (const candidate of candidates) {
    if (candidate.dynamic) {
      deny(`Cannot verify a shell-expanded GitHub PR title. Pass a literal --title value so commitlint can check it.`);
      return;
    }
    const result = await lintTitle(candidate.title);
    if (result.valid) {
      continue;
    }
    const errors = result.errors?.join("; ") || "title does not satisfy commitlint";
    const types = configuredValues(result.config, "type-enum").join(", ");
    const scopes = configuredValues(result.config, "scope-enum").join(", ");
    deny(`PR title rejected by commitlint: ${errors}. Allowed types: ${types}. Allowed scopes: ${scopes}. Header limit: 100 characters.`);
    return;
  }
}

function changedDocCommentLines(args) {
  const oldText = ["old_str", "old_string", "oldString", "old_text", "oldText"]
    .map((key) => args[key])
    .find((value) => typeof value === "string");
  const newText = ["new_str", "new_string", "newString", "new_text", "newText"]
    .map((key) => args[key])
    .find((value) => typeof value === "string");
  if (typeof oldText === "string" && typeof newText === "string") {
    return oldText.split(/\r?\n/)
      .filter((line) => /^\s*(?:\/\/\/|\/\*\*)/.test(line) && !newText.includes(line));
  }

  const patch = [args.patch, args.raw]
    .find((value) => typeof value === "string");
  if (typeof patch !== "string") {
    return [];
  }
  const deleted = patch.split(/\r?\n/)
    .filter((line) => /^-\s*(?:\/\/\/|\/\*\*)/.test(line))
    .map((line) => line.slice(1).trimStart());
  const added = patch.split(/\r?\n/)
    .filter((line) => /^\+\s*(?:\/\/\/|\/\*\*)/.test(line))
    .map((line) => line.slice(1).trimStart());
  return deleted.filter((line) => !added.includes(line));
}

function localePathsFromPatch(patch) {
  if (typeof patch !== "string") {
    return [];
  }
  const paths = [];
  const pattern = /^\*\*\* (?:Update|Add|Delete) File:\s*(.+)$/gm;
  for (const match of patch.matchAll(pattern)) {
    paths.push(match[1].trim());
  }
  return paths;
}

function localeFileForPath(event, filePath) {
  const normalized = normalizedPath(filePath).replace(/^(?:\.\/)+/, "");
  if (!/(?:^|\/)public\/i18n\/(?:en|nl|fr|de)\.json$/i.test(normalized)) {
    return "";
  }
  const base = typeof event.cwd === "string" ? event.cwd : repositoryRoot;
  if (path.isAbsolute(filePath)) {
    return path.resolve(filePath);
  }
  if (/^RefTestManagement\.Ui\/public\/i18n\//i.test(normalized)) {
    return path.resolve(repositoryRoot, normalized);
  }
  if (/^public\/i18n\//i.test(normalized)) {
    const normalizedBase = normalizedPath(base).replace(/\/+$/, "").toLowerCase();
    return normalizedBase.endsWith("/reftestmanagement.ui")
      ? path.resolve(base, normalized)
      : path.resolve(repositoryRoot, "RefTestManagement.Ui", normalized);
  }

  const fromCwd = path.resolve(base, filePath);
  const fromRepository = path.resolve(repositoryRoot, normalized);
  return existsSync(fromCwd) || !existsSync(fromRepository) ? fromCwd : fromRepository;
}

function localeFilesToCheck(event, toolName, args) {
  const tool = toolName.toLowerCase();
  const candidates = [];
  const filePath = getArgumentPath(args);
  if (filePath) {
    candidates.push(filePath);
  }
  candidates.push(...localePathsFromPatch(args.patch ?? args.raw));

  const command = shellCommandText(args);
  if ((tool === "bash" || tool === "powershell") && /(?:i18n|\.json\b)/i.test(command)) {
    for (const locale of ["en", "nl", "fr", "de"]) {
      candidates.push(path.join(repositoryRoot, "RefTestManagement.Ui", "public", "i18n", `${locale}.json`));
    }
  }

  return [...new Set(candidates
    .map((candidate) => localeFileForPath(event, candidate))
    .filter(Boolean))];
}

function validateLocale(filePath) {
  const displayPath = path.relative(repositoryRoot, filePath).replace(/\\/g, "/");
  if (!existsSync(filePath)) {
    return `Locale file ${displayPath} is missing after an edit.`;
  }

  let text;
  try {
    text = readFileSync(filePath, "utf8");
    JSON.parse(text);
  } catch (error) {
    return `Locale file ${displayPath} is not valid JSON (${errorMessage(error)}).`;
  }

  const lines = text.split(/\r?\n/);
  const indented = lines.filter((line) => line.trim().length > 0 && /^ +\S/.test(line));
  if (lines.length < 3) {
    return `Locale file ${displayPath} is no longer multiline.`;
  }
  if (!indented.some((line) => /^ {2}\S/.test(line))
    || indented.some((line) => /^ +\S/.test(line) && (line.match(/^ */)?.[0].length ?? 0) % 2 !== 0)
    || lines.some((line) => /^\t+\S/.test(line))) {
    return `Locale file ${displayPath} no longer uses consistent two-space indentation.`;
  }
  return "";
}

function postToolUse(event, toolName, args) {
  const warnings = [];
  if (["edit", "apply_patch", "str_replace_editor"].includes(toolName.toLowerCase())
    && changedDocCommentLines(args).length > 0) {
    warnings.push("A tool edit removed XML/JSDoc lines (`///` or `/**`). Check that the documentation remains accurate and was not removed accidentally.");
  }

  for (const filePath of localeFilesToCheck(event, toolName, args)) {
    const warning = validateLocale(filePath);
    if (warning) {
      warnings.push(`${warning} Preserve the file's multiline two-space formatting and de.json's existing \\u escapes; run npm run check:i18n.`);
    }
  }

  if (warnings.length > 0) {
    process.stdout.write(`${JSON.stringify({ additionalContext: warnings.join("\n") })}\n`);
  }
}

async function main() {
  try {
    const event = await readEvent();
    const action = process.argv[2];
    const toolName = getToolName(event);
    const args = getToolArguments(event);

    if (action === "pre") {
      await preToolUse(event, toolName, args);
    } else if (action === "post") {
      postToolUse(event, toolName, args);
    }
  } catch (error) {
    process.stderr.write(`[copilot guard] Could not process hook input; allowing the tool call: ${errorMessage(error)}\n`);
  }
}

await main();
