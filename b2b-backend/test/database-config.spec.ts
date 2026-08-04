import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ExecutionContext } from '@nestjs/common';
import { parseDatabaseSources, requireWriteDatabaseKey } from '../src/config/database-config';
import { AdminApiKeyGuard } from '../src/security/admin-api-key.guard';

describe('database configuration', () => {
  it('normalizes and validates configured sources', () => {
    const sources = parseDatabaseSources(
      JSON.stringify([
        { key: 'Main_DB', label: 'Main', uri: 'mongodb://localhost:27017/main' },
        { key: 'disabled', label: 'Disabled', uri: 'mongodb://localhost:27017/off', enabled: false },
      ]),
    );
    assert.deepEqual(sources, [
      { key: 'main_db', label: 'Main', uri: 'mongodb://localhost:27017/main', enabled: true },
      { key: 'disabled', label: 'Disabled', uri: 'mongodb://localhost:27017/off', enabled: false },
    ]);
  });

  it('rejects duplicate keys and non-Mongo URIs', () => {
    assert.throws(
      () => parseDatabaseSources(
        JSON.stringify([
          { key: 'same', label: 'One', uri: 'mongodb://localhost/one' },
          { key: 'same', label: 'Two', uri: 'mongodb://localhost/two' },
        ]),
      ),
      /Duplicate database key/,
    );
    assert.throws(
      () => parseDatabaseSources(JSON.stringify([{ key: 'bad', label: 'Bad', uri: 'https://example.com' }])),
      /invalid MongoDB URI/,
    );
  });

  it('requires a concrete write target', () => {
    assert.throws(() => requireWriteDatabaseKey(undefined), /specific databaseKey/);
    assert.throws(() => requireWriteDatabaseKey('all'), /specific databaseKey/);
    assert.equal(requireWriteDatabaseKey(' Main_DB '), 'main_db');
  });

  it('protects management routes with a constant-time API-key check', () => {
    const key = 'a-secure-management-key-123';
    const context = (supplied: string) =>
      ({
        switchToHttp: () => ({
          getRequest: () => ({ header: () => supplied }),
        }),
      }) as unknown as ExecutionContext;
    const guard = new AdminApiKeyGuard(key);
    assert.equal(guard.canActivate(context(key)), true);
    assert.throws(() => guard.canActivate(context('wrong')), /Invalid management API key/);
  });
});
