import {
  HttpException,
  Injectable,
  NotFoundException,
  OnApplicationShutdown,
  ServiceUnavailableException,
} from '@nestjs/common';
import { Db, MongoClient } from 'mongodb';
import { DatabaseSource, parseDatabaseSources } from '../config/database-config';

export type SourceResult<T> = {
  databaseKey: string;
  databaseLabel: string;
  data: T;
};

export type SourceError = {
  databaseKey: string;
  databaseLabel: string;
  error: string;
};

@Injectable()
export class ConnectionRegistryService implements OnApplicationShutdown {
  private readonly sources = parseDatabaseSources().filter((source) => source.enabled);
  private readonly clients = new Map<string, MongoClient>();
  private readonly connecting = new Map<string, Promise<MongoClient>>();
  private readonly concurrency = Math.max(1, Number(process.env.B2B_FANOUT_CONCURRENCY ?? 4));

  list(): Array<Omit<DatabaseSource, 'uri'>> {
    return this.sources.map(({ key, label, enabled }) => ({ key, label, enabled }));
  }

  source(key: string): DatabaseSource {
    const normalized = key.trim().toLowerCase();
    const source = this.sources.find((candidate) => candidate.key === normalized);
    if (!source) throw new NotFoundException(`Unknown or disabled databaseKey '${normalized}'`);
    return source;
  }

  async database(key: string): Promise<Db> {
    const source = this.source(key);
    const client = await this.client(source);
    return client.db();
  }

  async health() {
    return Promise.all(
      this.sources.map(async (source) => {
        const started = Date.now();
        try {
          const db = await this.database(source.key);
          await db.command({ ping: 1 });
          return { key: source.key, label: source.label, status: 'up' as const, latencyMs: Date.now() - started };
        } catch (error) {
          return {
            key: source.key,
            label: source.label,
            status: 'down' as const,
            latencyMs: Date.now() - started,
            error: this.safeError(error),
          };
        }
      }),
    );
  }

  async query<T>(
    requestedKey: string | undefined,
    operation: (db: Db, source: DatabaseSource) => Promise<T>,
  ): Promise<{ results: SourceResult<T>[]; errors: SourceError[] }> {
    const key = requestedKey?.trim().toLowerCase() || 'all';
    const isFanOut = key === 'all';
    const selected = key === 'all' ? this.sources : [this.source(key)];
    const results: SourceResult<T>[] = [];
    const errors: SourceError[] = [];
    let cursor = 0;

    const worker = async () => {
      while (cursor < selected.length) {
        const source = selected[cursor++];
        try {
          const db = await this.database(source.key);
          results.push({
            databaseKey: source.key,
            databaseLabel: source.label,
            data: await operation(db, source),
          });
        } catch (error) {
          if (!isFanOut && error instanceof HttpException) throw error;
          errors.push({
            databaseKey: source.key,
            databaseLabel: source.label,
            error: this.safeError(error),
          });
        }
      }
    };
    await Promise.all(Array.from({ length: Math.min(this.concurrency, selected.length) }, () => worker()));

    const sourceOrder = new Map(selected.map((source, index) => [source.key, index]));
    results.sort((a, b) => sourceOrder.get(a.databaseKey)! - sourceOrder.get(b.databaseKey)!);
    errors.sort((a, b) => sourceOrder.get(a.databaseKey)! - sourceOrder.get(b.databaseKey)!);
    if (!isFanOut && errors.length === 1) {
      throw new ServiceUnavailableException(errors[0].error);
    }
    return { results, errors };
  }

  async onApplicationShutdown() {
    await Promise.allSettled([...this.clients.values()].map((client) => client.close()));
  }

  private async client(source: DatabaseSource): Promise<MongoClient> {
    const existing = this.clients.get(source.key);
    if (existing) return existing;
    const pending = this.connecting.get(source.key);
    if (pending) return pending;

    const connection = MongoClient.connect(source.uri, {
      appName: 'rr-bridal-b2b-backend',
      maxPoolSize: Number(process.env.B2B_MONGO_MAX_POOL_SIZE ?? 10),
      serverSelectionTimeoutMS: Number(process.env.B2B_MONGO_TIMEOUT_MS ?? 5_000),
    })
      .then((client) => {
        this.clients.set(source.key, client);
        this.connecting.delete(source.key);
        return client;
      })
      .catch((error) => {
        this.connecting.delete(source.key);
        throw error;
      });
    this.connecting.set(source.key, connection);
    return connection;
  }

  private safeError(error: unknown): string {
    if (error instanceof Error) {
      if (/authentication/i.test(error.message)) return 'Database authentication failed';
      if (/timed out|server selection|ECONNREFUSED/i.test(error.message)) return 'Database is unavailable';
    }
    return 'Database operation failed';
  }
}
