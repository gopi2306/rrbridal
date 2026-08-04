import { BadRequestException } from '@nestjs/common';

export type DatabaseSource = {
  key: string;
  label: string;
  uri: string;
  publicApiBaseUrl?: string;
  enabled: boolean;
};

const KEY_PATTERN = /^[a-z][a-z0-9_-]{1,62}$/;

export function parseDatabaseSources(raw = process.env.B2B_DATABASES): DatabaseSource[] {
  if (!raw?.trim()) {
    throw new Error('B2B_DATABASES is required and must be a JSON array');
  }

  let value: unknown;
  try {
    value = JSON.parse(raw);
  } catch {
    throw new Error('B2B_DATABASES must contain valid JSON');
  }
  if (!Array.isArray(value) || value.length === 0) {
    throw new Error('B2B_DATABASES must be a non-empty JSON array');
  }

  const seen = new Set<string>();
  return value.map((entry, index) => {
    if (!entry || typeof entry !== 'object') {
      throw new Error(`B2B_DATABASES[${index}] must be an object`);
    }
    const record = entry as Record<string, unknown>;
    const key = typeof record.key === 'string' ? record.key.trim().toLowerCase() : '';
    const label = typeof record.label === 'string' ? record.label.trim() : '';
    const uri = typeof record.uri === 'string' ? record.uri.trim() : '';
    const publicApiBaseUrl =
      typeof record.publicApiBaseUrl === 'string'
        ? record.publicApiBaseUrl.trim().replace(/\/+$/, '')
        : undefined;
    const enabled = record.enabled !== false;

    if (!KEY_PATTERN.test(key)) throw new Error(`Invalid database key at index ${index}`);
    if (seen.has(key)) throw new Error(`Duplicate database key '${key}'`);
    if (!label) throw new Error(`Database '${key}' requires a label`);
    if (!/^mongodb(\+srv)?:\/\//i.test(uri)) throw new Error(`Database '${key}' has an invalid MongoDB URI`);
    if (publicApiBaseUrl && !/^https?:\/\//i.test(publicApiBaseUrl)) {
      throw new Error(`Database '${key}' has an invalid publicApiBaseUrl`);
    }
    seen.add(key);
    return { key, label, uri, ...(publicApiBaseUrl ? { publicApiBaseUrl } : {}), enabled };
  });
}

export function requireWriteDatabaseKey(value: string | undefined): string {
  const key = value?.trim().toLowerCase();
  if (!key || key === 'all') {
    throw new BadRequestException('A specific databaseKey is required for this operation');
  }
  return key;
}
