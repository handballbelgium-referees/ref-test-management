/**
 * Verifies that every locale file exposes exactly the same set of translation keys as English.
 *
 * A build cannot catch this class of bug. A locale file with a missing, misspelled or misplaced
 * key is still valid JSON and still compiles; the only symptom is a raw key rendered in the UI
 * for the users of that one language, which is precisely the audience least likely to report it.
 *
 * Run via `npm run check:i18n`. Exits non-zero on any divergence so it can gate CI.
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const I18N_DIR = join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'i18n');
const REFERENCE = 'en';
const LOCALES = ['en', 'nl', 'fr', 'de'];

/** Flattens a nested translation object into dotted key paths so two files can be compared. */
function flatten(value, prefix = '', out = new Map()) {
  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (child && typeof child === 'object' && !Array.isArray(child)) {
      flatten(child, path, out);
    } else {
      out.set(path, child);
    }
  }
  return out;
}

function load(locale) {
  return flatten(JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')));
}

const reference = load(REFERENCE);
const problems = [];
const withdrawalKeys = [
  'ref_test.withdraw_link',
  'ref_test.dialog.withdraw.message',
  'ref_test.dialog.withdraw.warning',
  'ref_test.dialog.withdraw.confirm',
];
const withdrawalCopyRules = {
  en: {
    required: [/consent/i, /anonym/i, /audit/i],
    forbidden: [/permanent(?:ly)?\s+delete/i, /delete\s+my\s+data/i],
  },
  nl: {
    required: [/toestemming/i, /anonim/i, /audit/i],
    forbidden: [/permanent(?:e| verwijderd)?/i, /gegevens verwijderen/i],
  },
  fr: {
    required: [/consentement/i, /anonym/i, /audit/i],
    forbidden: [/définitiv/i, /supprimer mes données/i],
  },
  de: {
    required: [/einwilligung/i, /anonym/i, /prüfzweck/i],
    forbidden: [/dauerhaft gelöscht/i, /daten löschen/i],
  },
};

for (const locale of LOCALES.filter((l) => l !== REFERENCE)) {
  const keys = load(locale);
  const missing = [...reference.keys()].filter((k) => !keys.has(k));
  const orphaned = [...keys.keys()].filter((k) => !reference.has(k));

  for (const key of missing) {
    problems.push(`${locale}: missing key "${key}" (present in ${REFERENCE})`);
  }
  for (const key of orphaned) {
    problems.push(`${locale}: orphaned key "${key}" (absent from ${REFERENCE})`);
  }

  console.log(
    `${locale}: ${keys.size} keys, ${missing.length} missing, ${orphaned.length} orphaned`,
  );
}

console.log(`${REFERENCE}: ${reference.size} keys (reference)`);

for (const locale of LOCALES) {
  const translations = load(locale);
  const copy = withdrawalKeys.map((key) => translations.get(key) ?? '').join(' ');
  const rules = withdrawalCopyRules[locale];

  for (const key of withdrawalKeys) {
    if (!translations.has(key)) {
      problems.push(`${locale}: withdrawal key "${key}" is missing`);
    }
  }
  for (const pattern of rules.required) {
    if (!pattern.test(copy)) {
      problems.push(`${locale}: withdrawal copy does not match required pattern ${pattern}`);
    }
  }
  for (const pattern of rules.forbidden) {
    if (pattern.test(copy)) {
      problems.push(`${locale}: withdrawal copy contains forbidden pattern ${pattern}`);
    }
  }
}

if (problems.length > 0) {
  console.error(`\n${problems.length} translation key problem(s):`);
  for (const problem of problems) {
    console.error(`  - ${problem}`);
  }
  process.exit(1);
}

console.log('\nAll locales expose an identical key set.');
