import assert from 'node:assert/strict';
import test from 'node:test';
import { evaluateFindings, parseAuditReport, validateExceptionPolicy } from './check-npm-audit.mjs';

const peerAdvisory = {
  source: 123456,
  title: 'Peer dependency issue',
  severity: 'high',
  url: 'https://github.com/advisories/GHSA-aaaa-bbbb-cccc',
  cves: ['CVE-2026-12345'],
};

test('parses high and critical advisories without excluding peer or development dependencies', () => {
  const findings = parseAuditReport({
    auditReportVersion: 2,
    vulnerabilities: {
      'peer-package': {
        severity: 'high',
        isDirect: false,
        isPeer: true,
        dev: true,
        via: [peerAdvisory],
      },
      'critical-package': {
        severity: 'critical',
        isDirect: true,
        via: [{ source: 987654, severity: 'critical', title: 'Critical issue' }],
      },
      'github-advisory-package': {
        severity: 'high',
        via: [{ severity: 'high', url: 'https://github.com/advisories/GHSA-DDDD-EEEE-FFFF' }],
      },
      'moderate-package': {
        severity: 'moderate',
        via: [{ source: 987655, severity: 'moderate' }],
      },
    },
  }, 'root');

  assert.deepEqual(findings.map(({ package: name, severity, id }) => ({ name, severity, id })), [
    { name: 'peer-package', severity: 'high', id: '123456' },
    { name: 'critical-package', severity: 'critical', id: '987654' },
    { name: 'github-advisory-package', severity: 'high', id: 'GHSA-DDDD-EEEE-FFFF' },
  ]);
  assert.deepEqual(findings[0].ids, ['123456', 'GHSA-AAAA-BBBB-CCCC', 'CVE-2026-12345']);
});

test('matches documented advisory aliases when npm reports a numeric source ID', () => {
  const finding = parseAuditReport({
    vulnerabilities: {
      'peer-package': {
        severity: 'high',
        via: [peerAdvisory],
      },
    },
  })[0];

  for (const id of ['GHSA-AAAA-BBBB-CCCC', 'CVE-2026-12345']) {
    const exceptions = validateExceptionPolicy({
      exceptions: [{
        id,
        rationale: 'Upstream patch is pending and is tracked for the next maintenance release.',
        owner: '@maintainer',
        expiresOn: '2026-10-31',
      }],
    }, '2026-10-07');
    assert.deepEqual(evaluateFindings([finding], exceptions), {
      unexcepted: [],
      unusedExceptions: [],
    });
  }
});

test('uses the vulnerable dependency advisory identity rather than inherited package paths', () => {
  const findings = parseAuditReport({
    vulnerabilities: {
      'affected-parent': { severity: 'high', via: ['vulnerable-leaf'] },
      'vulnerable-leaf': { severity: 'high', via: [peerAdvisory, 'affected-parent'] },
    },
  });

  assert.equal(findings.length, 1);
  assert.equal(findings[0].package, 'vulnerable-leaf');
  assert.equal(findings[0].id, '123456');
});

test('keeps high findings without an advisory identity blocking', () => {
  const findings = parseAuditReport({
    vulnerabilities: {
      'unknown-high-package': { severity: 'high', via: [] },
      'orphaned-parent': { severity: 'high', via: ['missing-leaf'] },
    },
  });

  assert.equal(findings[0].id, null);
  assert.equal(findings[1].id, null);
});

test('rejects malformed npm audit reports', () => {
  assert.throws(() => parseAuditReport('{not json'), /valid JSON/);
  assert.throws(() => parseAuditReport({ vulnerabilities: { broken: { severity: 'high' } } }), /advisory list/);
});

test('validates required exception identity, rationale, owner, and expiry', () => {
  const policy = {
    exceptions: [{
      id: '123456',
      rationale: 'Upstream patch is pending and is tracked for the next maintenance release.',
      owner: '@handballbelgium-referees/security',
      expiresOn: '2026-10-31',
    }],
  };

  assert.equal(validateExceptionPolicy(policy, '2026-10-07').has('123456'), true);
  assert.throws(() => validateExceptionPolicy(policy, '2026-11-01'), /expired/);
  assert.throws(() => validateExceptionPolicy({
    exceptions: [{ ...policy.exceptions[0], owner: '' }],
  }, '2026-10-07'), /owner/);
  assert.throws(() => validateExceptionPolicy({
    exceptions: [{ ...policy.exceptions[0], expiresOn: '2026-02-30' }],
  }, '2026-10-07'), /valid YYYY-MM-DD/);
});

test('only exact unexpired advisory exceptions pass; new and unidentified advisories block', () => {
  const exceptions = validateExceptionPolicy({
    exceptions: [{
      id: '123456',
      rationale: 'Upstream patch is pending and is tracked for the next maintenance release.',
      owner: '@maintainer',
      expiresOn: '2026-10-31',
    }],
  }, '2026-10-07');
  const result = evaluateFindings([
    { severity: 'high', id: '123456' },
    { severity: 'critical', id: 'GHSA-DDDD-EEEE-FFFF', project: 'ui', package: 'new-advisory' },
    { severity: 'high', id: null, project: 'ui', package: 'unknown-advisory' },
  ], exceptions);

  assert.deepEqual(result.unexcepted.map((item) => item.id), ['GHSA-DDDD-EEEE-FFFF', null]);
  assert.deepEqual(result.unusedExceptions, []);
  assert.deepEqual(evaluateFindings([], exceptions).unusedExceptions, ['123456']);
});

test('rejects stale, duplicate, and malformed exceptions', () => {
  const exception = {
    id: '123456',
    rationale: 'Upstream patch is pending and is tracked for the next maintenance release.',
    owner: '@maintainer',
    expiresOn: '2026-10-31',
  };

  assert.throws(() => validateExceptionPolicy({ exceptions: [exception, exception] }, '2026-10-07'), /Duplicate/);
  assert.throws(() => validateExceptionPolicy({ exceptions: [{ ...exception, id: 'bad-id' }] }, '2026-10-07'), /identity/);
  assert.throws(() => validateExceptionPolicy({ exceptions: [{ ...exception, rationale: '' }] }, '2026-10-07'), /rationale/);
});
