/**
 * Self-test: zip packaging for billing client download (no Mongo / Nest bootstrap).
 * Run: npx ts-node -r reflect-metadata src/modules/stores/billing-client-package.selftest.ts
 */
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';
import { BillingClientPackageService } from './billing-client-package.service';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

async function main() {
  const artifactDir = mkdtempSync(join(tmpdir(), 'rr-billing-artifact-'));
  const singleDir = mkdtempSync(join(tmpdir(), 'rr-billing-single-'));
  const previousArtifact = process.env.BILLING_CLIENT_ARTIFACT_DIR;
  const previousSingle = process.env.BILLING_CLIENT_SINGLE_EXE_PATH;
  const previousApi = process.env.PUBLIC_CENTRAL_API_BASE;
  const previousOrigin = process.env.API_PUBLIC_ORIGIN;

  try {
    writeFileSync(join(artifactDir, 'RRBridal.StoreBilling.App.exe'), Buffer.from('fake-folder-exe'));
    writeFileSync(join(artifactDir, 'readme.txt'), 'hello');
    mkdirSync(join(artifactDir, 'subdir'));
    writeFileSync(join(artifactDir, 'subdir', 'nested.dll'), Buffer.from('dll'));

    const singleExePath = join(singleDir, 'RRBridal.StoreBilling.App.exe');
    writeFileSync(singleExePath, Buffer.from('fake-single-exe'));

    process.env.BILLING_CLIENT_ARTIFACT_DIR = artifactDir;
    process.env.BILLING_CLIENT_SINGLE_EXE_PATH = singleExePath;
    delete process.env.PUBLIC_CENTRAL_API_BASE;
    process.env.API_PUBLIC_ORIGIN = 'https://bridaldev.rrbazaar.in/';

    const storesService = {
      findByCode: async (code: string) => ({
        code: code.trim().toLowerCase(),
        preferCentralOnline: true,
      }),
    };

    const service = new BillingClientPackageService(storesService as never);

    // Uses API_PUBLIC_ORIGIN when PUBLIC_CENTRAL_API_BASE unset
    assert(
      service.resolvePublicApiBase('http://ignored:3000') === 'https://bridaldev.rrbazaar.in',
      'should prefer API_PUBLIC_ORIGIN over request',
    );
    process.env.PUBLIC_CENTRAL_API_BASE = 'https://override.example/';
    assert(
      service.resolvePublicApiBase('http://ignored:3000') === 'https://override.example',
      'should prefer PUBLIC_CENTRAL_API_BASE',
    );
    delete process.env.PUBLIC_CENTRAL_API_BASE;
    delete process.env.API_PUBLIC_ORIGIN;
    assert(
      service.resolvePublicApiBase('http://localhost:3000') === 'http://localhost:3000',
      'should fall back to request host',
    );
    process.env.API_PUBLIC_ORIGIN = 'https://bridaldev.rrbazaar.in/';

    // format=zip (default)
    const zipResult = await service.buildPackage('Store-001', 2, 'zip');
    assert(
      zipResult.filename === 'RRBridal-StoreBilling-store-001-counter-02.zip',
      `zip filename=${zipResult.filename}`,
    );
    const zip = readFileSync(zipResult.zipPath);
    assert(zip.length > 4 && zip.readUInt32LE(0) === 0x04034b50, 'zip local header magic');
    assert(zip.includes(Buffer.from('.env')), 'zip should include .env');
    assert(zip.includes(Buffer.from('fake-folder-exe')), 'zip should include folder EXE bytes');
    assert(zip.includes(Buffer.from('nested.dll')), 'zip should include nested dll');
    assert(zip.includes(Buffer.from('CENTRAL_API_BASE=https://bridaldev.rrbazaar.in')), 'env api base');
    assert(zip.includes(Buffer.from('STORE_ID=store-001')), 'store id');
    await zipResult.cleanup();

    // format=exe
    const exeResult = await service.buildPackage('Store-001', 2, 'exe');
    assert(
      exeResult.filename === 'RRBridal-StoreBilling-store-001-counter-02-exe.zip',
      `exe filename=${exeResult.filename}`,
    );
    const exeZip = readFileSync(exeResult.zipPath);
    assert(exeZip.includes(Buffer.from('fake-single-exe')), 'exe zip should include single EXE bytes');
    assert(exeZip.includes(Buffer.from('.env')), 'exe zip should include .env');
    assert(!exeZip.includes(Buffer.from('nested.dll')), 'exe zip must not include folder DLLs');
    assert(exeZip.includes(Buffer.from('DEVICE_ID=counter-02')), 'device id');
    assert(exeZip.includes(Buffer.from('POS_COUNTER=2')), 'pos counter');
    assert(exeZip.includes(Buffer.from('PREFER_CENTRAL_ONLINE=true')), 'prefer online');
    await exeResult.cleanup();

    console.log('billing-client-package.selftest: ok');
  } finally {
    rmSync(artifactDir, { recursive: true, force: true });
    rmSync(singleDir, { recursive: true, force: true });
    if (previousArtifact === undefined) delete process.env.BILLING_CLIENT_ARTIFACT_DIR;
    else process.env.BILLING_CLIENT_ARTIFACT_DIR = previousArtifact;
    if (previousSingle === undefined) delete process.env.BILLING_CLIENT_SINGLE_EXE_PATH;
    else process.env.BILLING_CLIENT_SINGLE_EXE_PATH = previousSingle;
    if (previousApi === undefined) delete process.env.PUBLIC_CENTRAL_API_BASE;
    else process.env.PUBLIC_CENTRAL_API_BASE = previousApi;
    if (previousOrigin === undefined) delete process.env.API_PUBLIC_ORIGIN;
    else process.env.API_PUBLIC_ORIGIN = previousOrigin;
  }
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
