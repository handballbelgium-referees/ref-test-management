import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const projects = [
  { name: 'root', directory: root },
  { name: 'RefTestManagement.Ui', directory: path.join(root, 'RefTestManagement.Ui') },
];
const severities = new Set(['info', 'low', 'moderate', 'high', 'critical']);
const blockingSeverities = new Set(['high', 'critical']);
const advisoryIdPattern = /^(?:[1-9]\d*|GHSA-[A-Z0-9]{4}(?:-[A-Z0-9]{4}){2}|CVE-\d{4}-\d{4,})$/;
const ownerPattern = /^@[A-Za-z0-9][A-Za-z0-9-]{0,38}(?:\/[A-Za-z0-9][A-Za-z0-9-]{0,38})?$/;
const requiredExceptionFields = ['expiresOn', 'id', 'owner', 'rationale'];

function isRecord(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function normalizedAdvisoryId(value) {
  if (Number.isSafeInteger(value) && value > 0) return String(value);
  if (typeof value !== 'string') return null;

  const id = value.trim().toUpperCase();
  return advisoryIdPattern.test(id) ? id : null;
}

function advisoryIds(advisory, fallback) {
  const ids = new Set();
  for (const value of [advisory.source, advisory.id, fallback]) {
    const id = normalizedAdvisoryId(value);
    if (id) ids.add(id);
  }

  if (typeof advisory.url === 'string') {
    for (const match of advisory.url.matchAll(/GHSA-[A-Z0-9]{4}(?:-[A-Z0-9]{4}){2}|CVE-\d{4}-\d{4,}/gi)) {
      const id = normalizedAdvisoryId(match[0]);
      if (id) ids.add(id);
    }
    const npmId = advisory.url.match(/\/advisories\/([1-9]\d*)(?:[?#]|$)/i)?.[1];
    const id = normalizedAdvisoryId(npmId);
    if (id) ids.add(id);
  }

  const aliases = [advisory.cve];
  if (Array.isArray(advisory.cves)) aliases.push(...advisory.cves);
  if (Array.isArray(advisory.aliases)) aliases.push(...advisory.aliases);
  for (const value of aliases) {
    const id = normalizedAdvisoryId(value);
    if (id) ids.add(id);
  }

  return [...ids];
}

function finding(project, packageName, severity, ids, advisory = {}) {
  return {
    project,
    package: packageName,
    severity,
    id: ids[0] ?? null,
    ids,
    title: typeof advisory.title === 'string' ? advisory.title : '',
  };
}

export function parseAuditReport(input, project = 'unknown') {
  let report = input;
  if (typeof input === 'string') {
    try {
      report = JSON.parse(input);
    } catch {
      throw new Error('npm audit did not return valid JSON.');
    }
  }
  if (!isRecord(report)) throw new Error('npm audit returned an invalid report.');

  const findings = [];
  if (Object.hasOwn(report, 'vulnerabilities')) {
    if (!isRecord(report.vulnerabilities)) throw new Error('npm audit returned an invalid vulnerabilities map.');

    const vulnerabilities = new Map(Object.entries(report.vulnerabilities));
    for (const [packageName, vulnerability] of Object.entries(report.vulnerabilities)) {
      if (!isRecord(vulnerability) || !severities.has(vulnerability.severity)) {
        throw new Error(`npm audit returned an invalid vulnerability for ${packageName}.`);
      }
      if (!Array.isArray(vulnerability.via)) {
        throw new Error(`npm audit returned an invalid advisory list for ${packageName}.`);
      }

      for (const item of vulnerability.via) {
        if (typeof item !== 'string' && (!isRecord(item) || !severities.has(item.severity))) {
          throw new Error(`npm audit returned malformed advisory data for ${packageName}.`);
        }
      }
    }

    const directAdvisories = new Set();
    for (const [packageName, vulnerability] of vulnerabilities) {
      for (const advisory of vulnerability.via) {
        if (typeof advisory !== 'string' && blockingSeverities.has(advisory.severity)) {
          directAdvisories.add(packageName);
          findings.push(finding(project, packageName, advisory.severity, advisoryIds(advisory), advisory));
        }
      }
    }

    const inspected = new Set();
    for (const [packageName, vulnerability] of vulnerabilities) {
      if (!blockingSeverities.has(vulnerability.severity) || inspected.has(packageName)) continue;

      const relatedPackages = new Set();
      const pending = [packageName];
      let hasUnresolvedReference = false;
      while (pending.length > 0) {
        const currentName = pending.pop();
        if (relatedPackages.has(currentName)) continue;
        relatedPackages.add(currentName);
        inspected.add(currentName);

        const current = vulnerabilities.get(currentName);
        for (const item of current.via) {
          if (typeof item !== 'string') continue;
          const related = vulnerabilities.get(item);
          if (!related) {
            hasUnresolvedReference = true;
            findings.push(finding(project, currentName, current.severity, []));
          } else if (blockingSeverities.has(related.severity)) {
            pending.push(item);
          }
        }
      }

      // Package-name references can form cycles; accept them only when the reachable graph
      // contains a direct high-severity advisory. Unresolved or unexplained paths stay blocking.
      if (
        !hasUnresolvedReference &&
        ![...relatedPackages].some((name) => directAdvisories.has(name))
      ) {
        findings.push(finding(project, packageName, vulnerability.severity, []));
      }
    }
    return findings;
  }

  if (Object.hasOwn(report, 'advisories')) {
    if (!isRecord(report.advisories)) throw new Error('npm audit returned an invalid advisories map.');
    for (const [key, advisory] of Object.entries(report.advisories)) {
      if (!isRecord(advisory) || !severities.has(advisory.severity)) {
        throw new Error(`npm audit returned malformed advisory data for ${key}.`);
      }
      if (blockingSeverities.has(advisory.severity)) {
        findings.push(finding(
          project,
          typeof advisory.module_name === 'string' ? advisory.module_name : 'unknown package',
          advisory.severity,
          advisoryIds(advisory, key),
          advisory,
        ));
      }
    }
    return findings;
  }

  throw new Error('npm audit report has no supported vulnerabilities or advisories map.');
}

export function validateExceptionPolicy(policy, today = new Date().toISOString().slice(0, 10)) {
  if (!isRecord(policy) || Object.keys(policy).length !== 1 || !Array.isArray(policy.exceptions)) {
    throw new Error('The audit exception policy must contain only an exceptions array.');
  }

  const exceptions = new Map();
  for (const [index, exception] of policy.exceptions.entries()) {
    if (!isRecord(exception) || Object.keys(exception).sort().join(',') !== requiredExceptionFields.join(',')) {
      throw new Error(`Exception ${index + 1} must contain exactly id, rationale, owner, and expiresOn.`);
    }

    const id = normalizedAdvisoryId(exception.id);
    if (!id) throw new Error(`Exception ${index + 1} has an invalid advisory identity.`);
    if (exceptions.has(id)) throw new Error(`Duplicate audit exception for ${id}.`);
    if (typeof exception.rationale !== 'string' || exception.rationale.trim().length < 20) {
      throw new Error(`Exception ${id} must include a rationale of at least 20 characters.`);
    }
    if (typeof exception.owner !== 'string' || !ownerPattern.test(exception.owner)) {
      throw new Error(`Exception ${id} must name an owner as @user or @org/team.`);
    }
    if (
      typeof exception.expiresOn !== 'string' ||
      !/^\d{4}-\d{2}-\d{2}$/.test(exception.expiresOn) ||
      new Date(`${exception.expiresOn}T00:00:00.000Z`).toISOString().slice(0, 10) !== exception.expiresOn
    ) {
      throw new Error(`Exception ${id} must have a valid YYYY-MM-DD expiry date.`);
    }
    if (exception.expiresOn < today) throw new Error(`Audit exception ${id} expired on ${exception.expiresOn}.`);

    exceptions.set(id, exception);
  }

  return exceptions;
}

export function evaluateFindings(findings, exceptions) {
  const unexcepted = [];
  const usedExceptions = new Set();
  for (const item of findings) {
    if (!blockingSeverities.has(item.severity)) continue;
    const matchedId = (item.ids ?? (item.id ? [item.id] : [])).find((id) => exceptions.has(id));
    if (matchedId) usedExceptions.add(matchedId);
    else unexcepted.push(item);
  }

  return {
    unexcepted,
    unusedExceptions: [...exceptions.keys()].filter((id) => !usedExceptions.has(id)),
  };
}

function runAudit(project) {
  const windows = process.platform === 'win32';
  const command = windows ? (process.env.ComSpec || 'cmd.exe') : 'npm';
  const args = windows
    ? ['/d', '/s', '/c', 'npm.cmd audit --json --audit-level=high --include=dev --include=optional --include=peer']
    : ['audit', '--json', '--audit-level=high', '--include=dev', '--include=optional', '--include=peer'];
  const result = spawnSync(command, args, {
    cwd: project.directory,
    encoding: 'utf8',
    maxBuffer: 20 * 1024 * 1024,
  });

  if (result.error) throw new Error(`${project.name}: could not start npm audit (${result.error.message}).`);
  if (result.signal) throw new Error(`${project.name}: npm audit was terminated by ${result.signal}.`);

  let report;
  try {
    report = JSON.parse(result.stdout);
  } catch {
    throw new Error(`${project.name}: npm audit did not return valid JSON (exit ${result.status ?? 'unknown'}).`);
  }

  const findings = parseAuditReport(report, project.name);
  if (result.status !== 0 && (result.status !== 1 || findings.length === 0)) {
    throw new Error(`${project.name}: npm audit failed unexpectedly (exit ${result.status ?? 'unknown'}).`);
  }
  return { project: project.name, report, findings };
}

function readPolicy() {
  const policyPath = path.join(root, '.github', 'npm-audit-exceptions.json');
  return validateExceptionPolicy(JSON.parse(readFileSync(policyPath, 'utf8')));
}

function reportCounts(audit) {
  const counts = audit.report.metadata?.vulnerabilities;
  if (!isRecord(counts)) return `${audit.findings.length} direct High/Critical advisories`;
  return `${counts.high ?? 0} High, ${counts.critical ?? 0} Critical`;
}

function main() {
  let exceptions;
  let policyError;
  try {
    exceptions = readPolicy();
  } catch (error) {
    policyError = error;
  }

  const audits = [];
  const auditErrors = [];
  for (const project of projects) {
    try {
      audits.push(runAudit(project));
    } catch (error) {
      auditErrors.push(error);
    }
  }

  for (const audit of audits) {
    console.log(`${audit.project}: ${reportCounts(audit)} reported by npm audit.`);
  }
  if (policyError) console.error(`Invalid npm audit exception policy: ${policyError.message}`);
  for (const error of auditErrors) console.error(error.message);
  if (policyError || auditErrors.length > 0) {
    process.exitCode = 1;
    return;
  }

  const result = evaluateFindings(audits.flatMap((audit) => audit.findings), exceptions);
  for (const item of result.unexcepted) {
    const identity = item.ids.length > 0 ? item.ids.join(', ') : 'unidentified advisory';
    console.error(`Unexcepted ${item.severity}: ${item.project} / ${item.package} (${identity})`);
  }
  for (const id of result.unusedExceptions) {
    console.error(`Unused npm audit exception: ${id}`);
  }
  if (result.unexcepted.length > 0 || result.unusedExceptions.length > 0) {
    process.exitCode = 1;
    return;
  }

  console.log('All root and Angular High/Critical npm advisories are covered by the current policy.');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
