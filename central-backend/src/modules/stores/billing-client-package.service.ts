import { Injectable, ServiceUnavailableException } from '@nestjs/common';
import { createWriteStream, existsSync } from 'fs';
import { mkdtemp, readdir, readFile, rm, stat } from 'fs/promises';
import { tmpdir } from 'os';
import { join, relative, resolve, sep } from 'path';
import { once } from 'events';
import { StoresService } from './stores.service';

const EXE_NAME = 'RRBridal.StoreBilling.App.exe';

export type BillingClientFormat = 'exe' | 'zip';

const CRC_TABLE = (() => {
  const table = new Uint32Array(256);
  for (let i = 0; i < 256; i++) {
    let c = i;
    for (let j = 0; j < 8; j++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[i] = c >>> 0;
  }
  return table;
})();

function crc32(buf: Buffer): number {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) {
    c = CRC_TABLE[(c ^ buf[i]!) & 0xff]! ^ (c >>> 8);
  }
  return (c ^ 0xffffffff) >>> 0;
}

export type BillingClientPackageResult = {
  zipPath: string;
  filename: string;
  cleanup: () => Promise<void>;
};

type ZipEntry = {
  name: string;
  data: Buffer;
  crc: number;
  localHeaderOffset: number;
};

@Injectable()
export class BillingClientPackageService {
  constructor(private readonly storesService: StoresService) {}

  async buildPackage(
    storeCode: string,
    posCounter: number,
    format: BillingClientFormat = 'zip',
    requestApiBase?: string,
  ): Promise<BillingClientPackageResult> {
    const store = await this.storesService.findByCode(storeCode);

    const publicApiBase = this.resolvePublicApiBase(requestApiBase);

    const code = String(store.code);
    const padded = String(posCounter).padStart(2, '0');
    const deviceId = `counter-${padded}`;
    const preferCentralOnline = store.preferCentralOnline === true;
    const envContents = [
      `STORE_ID=${code}`,
      `DEVICE_ID=${deviceId}`,
      `POS_COUNTER=${posCounter}`,
      `CENTRAL_API_BASE=${publicApiBase}`,
      `PREFER_CENTRAL_ONLINE=${preferCentralOnline ? 'true' : 'false'}`,
      '',
    ].join('\n');

    const workDir = await mkdtemp(join(tmpdir(), 'rr-billing-client-'));
    const zipPath = join(workDir, 'package.zip');
    const filename =
      format === 'exe'
        ? `RRBridal-StoreBilling-${code}-counter-${padded}-exe.zip`
        : `RRBridal-StoreBilling-${code}-counter-${padded}.zip`;

    try {
      if (format === 'exe') {
        await this.writeExeZip(envContents, zipPath);
      } else {
        await this.writeFolderZip(envContents, zipPath);
      }
    } catch (err) {
      await rm(workDir, { recursive: true, force: true }).catch(() => undefined);
      throw err;
    }

    return {
      zipPath,
      filename,
      cleanup: async () => {
        await rm(workDir, { recursive: true, force: true }).catch(() => undefined);
      },
    };
  }

  /**
   * CENTRAL_API_BASE for till .env, in order:
   * 1) PUBLIC_CENTRAL_API_BASE (explicit override)
   * 2) API_PUBLIC_ORIGIN (shared deployment public URL)
   * 3) requestApiBase from the download request (Swagger / TruStock host)
   */
  resolvePublicApiBase(requestApiBase?: string): string {
    const fromEnv = (process.env.PUBLIC_CENTRAL_API_BASE ?? '').trim().replace(/\/+$/, '');
    if (fromEnv) return fromEnv;

    const fromDeployment = (process.env.API_PUBLIC_ORIGIN ?? '').trim().replace(/\/+$/, '');
    if (fromDeployment) return fromDeployment;

    const fromRequest = (requestApiBase ?? '').trim().replace(/\/+$/, '');
    if (fromRequest) return fromRequest;

    throw new ServiceUnavailableException(
      'No public API base for till .env. Set API_PUBLIC_ORIGIN (or PUBLIC_CENTRAL_API_BASE), ' +
        'or call the download API via the public host so the request URL can be used.',
    );
  }

  private resolveArtifactDir(): string {
    const raw = (process.env.BILLING_CLIENT_ARTIFACT_DIR ?? '').trim();
    if (!raw) {
      throw new ServiceUnavailableException(
        'BILLING_CLIENT_ARTIFACT_DIR is not set. Point it at the prebuilt self-contained win-x64 publish folder on this host.',
      );
    }
    return resolve(raw);
  }

  private resolveSingleExePath(): string {
    const raw = (process.env.BILLING_CLIENT_SINGLE_EXE_PATH ?? '').trim();
    if (!raw) {
      throw new ServiceUnavailableException(
        'BILLING_CLIENT_SINGLE_EXE_PATH is not set. Point it at the PublishSingleFile RRBridal.StoreBilling.App.exe from publish-store-billing-client.ps1.',
      );
    }
    return resolve(raw);
  }

  private async writeFolderZip(envContents: string, zipPath: string): Promise<void> {
    const artifactDir = this.resolveArtifactDir();
    const exePath = join(artifactDir, EXE_NAME);
    if (!existsSync(exePath)) {
      throw new ServiceUnavailableException(
        `Billing client artifact is not deployed. Expected ${EXE_NAME} under BILLING_CLIENT_ARTIFACT_DIR (${artifactDir}). ` +
          'Publish on Windows with store-billing-wpf/scripts/publish-store-billing-client.ps1 and sync the folder to this host.',
      );
    }

    const files = await this.collectFiles(artifactDir);
    const entries: Array<{ name: string; data: Buffer }> = [];

    for (const abs of files) {
      const rel = relative(artifactDir, abs).split(sep).join('/');
      if (!rel || rel === '.env') continue;
      entries.push({ name: rel, data: await readFile(abs) });
    }
    entries.push({ name: '.env', data: Buffer.from(envContents, 'utf8') });
    await this.writeZipEntries(entries, zipPath);
  }

  private async writeExeZip(envContents: string, zipPath: string): Promise<void> {
    const singleExePath = this.resolveSingleExePath();
    if (!existsSync(singleExePath)) {
      throw new ServiceUnavailableException(
        `Single-file billing client EXE not found at BILLING_CLIENT_SINGLE_EXE_PATH (${singleExePath}). ` +
          'Publish on Windows with store-billing-wpf/scripts/publish-store-billing-client.ps1 and copy the -single EXE to this host.',
      );
    }

    const exeBytes = await readFile(singleExePath);
    const entries: Array<{ name: string; data: Buffer }> = [
      { name: EXE_NAME, data: exeBytes },
      { name: '.env', data: Buffer.from(envContents, 'utf8') },
    ];
    await this.writeZipEntries(entries, zipPath);
  }

  /** Uncompressed ZIP (STORE) — fine for already-compressed .NET publish output; no ESM zip deps. */
  private async writeZipEntries(
    entries: Array<{ name: string; data: Buffer }>,
    zipPath: string,
  ): Promise<void> {
    const output = createWriteStream(zipPath);
    const written: ZipEntry[] = [];
    let offset = 0;

    const writeBuf = async (buf: Buffer) => {
      if (!output.write(buf)) await once(output, 'drain');
      offset += buf.length;
    };

    for (const entry of entries) {
      const nameBuf = Buffer.from(entry.name, 'utf8');
      const crc = crc32(entry.data);
      const localHeaderOffset = offset;
      const localHeader = Buffer.alloc(30);
      localHeader.writeUInt32LE(0x04034b50, 0);
      localHeader.writeUInt16LE(20, 4); // version needed
      localHeader.writeUInt16LE(0, 6); // flags
      localHeader.writeUInt16LE(0, 8); // method STORE
      localHeader.writeUInt16LE(0, 10); // time
      localHeader.writeUInt16LE(0, 12); // date
      localHeader.writeUInt32LE(crc >>> 0, 14);
      localHeader.writeUInt32LE(entry.data.length, 18);
      localHeader.writeUInt32LE(entry.data.length, 22);
      localHeader.writeUInt16LE(nameBuf.length, 26);
      localHeader.writeUInt16LE(0, 28); // extra length
      await writeBuf(localHeader);
      await writeBuf(nameBuf);
      await writeBuf(entry.data);
      written.push({ name: entry.name, data: entry.data, crc: crc >>> 0, localHeaderOffset });
    }

    const centralStart = offset;
    for (const entry of written) {
      const nameBuf = Buffer.from(entry.name, 'utf8');
      const central = Buffer.alloc(46);
      central.writeUInt32LE(0x02014b50, 0);
      central.writeUInt16LE(20, 4);
      central.writeUInt16LE(20, 6);
      central.writeUInt16LE(0, 8);
      central.writeUInt16LE(0, 10);
      central.writeUInt16LE(0, 12);
      central.writeUInt16LE(0, 14);
      central.writeUInt32LE(entry.crc, 16);
      central.writeUInt32LE(entry.data.length, 20);
      central.writeUInt32LE(entry.data.length, 24);
      central.writeUInt16LE(nameBuf.length, 28);
      central.writeUInt16LE(0, 30);
      central.writeUInt16LE(0, 32);
      central.writeUInt16LE(0, 34);
      central.writeUInt16LE(0, 36);
      central.writeUInt32LE(0, 38);
      central.writeUInt32LE(entry.localHeaderOffset, 42);
      await writeBuf(central);
      await writeBuf(nameBuf);
    }
    const centralSize = offset - centralStart;

    const end = Buffer.alloc(22);
    end.writeUInt32LE(0x06054b50, 0);
    end.writeUInt16LE(0, 4);
    end.writeUInt16LE(0, 6);
    end.writeUInt16LE(written.length, 8);
    end.writeUInt16LE(written.length, 10);
    end.writeUInt32LE(centralSize, 12);
    end.writeUInt32LE(centralStart, 16);
    end.writeUInt16LE(0, 20);
    await writeBuf(end);

    output.end();
    await once(output, 'close');
  }

  private async collectFiles(dir: string): Promise<string[]> {
    const out: string[] = [];
    const entries = await readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) {
        out.push(...(await this.collectFiles(full)));
      } else if (entry.isFile()) {
        const st = await stat(full);
        if (st.isFile()) out.push(full);
      }
    }
    return out;
  }
}
