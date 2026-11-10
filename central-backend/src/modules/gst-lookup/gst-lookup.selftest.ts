/**
 * Self-test: GSTIN validation and taxpayer payload mapping (no HTTP / Nest bootstrap).
 * Run: npx ts-node -r reflect-metadata src/modules/gst-lookup/gst-lookup.selftest.ts
 */
import { BadRequestException, ServiceUnavailableException } from '@nestjs/common';
import {
  isValidGstin,
  mapTaxpayerPayload,
  requireApiKey,
  requireValidGstin,
  resolveGstLookupBaseUrl,
  buildGstLookupUrl,
  DEFAULT_GST_LOOKUP_BASE_URL,
} from './gst-lookup.mapper';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

function expectThrow(fn: () => unknown, kind: new (...args: never[]) => Error, msg: string) {
  try {
    fn();
  } catch (err) {
    assert(err instanceof kind, `${msg}: expected ${kind.name}`);
    return;
  }
  throw new Error(`${msg}: expected throw`);
}

const sample = {
  flag: true,
  message: 'GSTIN found.',
  data: {
    gstin: '36AABCU9603R1ZM',
    lgnm: 'RR BRIDAL PRIVATE LIMITED',
    tradeNam: 'RR Bridal',
    sts: 'Active',
    pradr: {
      addr: {
        bno: '12-3',
        flno: '2nd Floor',
        bnm: 'Bridal Plaza',
        st: 'Road No 1',
        loc: 'Madhapur',
        dst: 'Hyderabad',
        stcd: '36',
        pncd: '500081',
      },
    },
  },
};

function main() {
  assert(isValidGstin('36AABCU9603R1ZM'), 'valid GSTIN should pass');
  assert(!isValidGstin('36INVALID'), 'short GSTIN should fail');
  assert(!isValidGstin(''), 'empty GSTIN should fail');
  expectThrow(() => requireValidGstin('bad'), BadRequestException, 'invalid GSTIN');
  expectThrow(() => requireApiKey(''), ServiceUnavailableException, 'missing API key');
  expectThrow(() => requireApiKey('   '), ServiceUnavailableException, 'blank API key');
  assert(requireApiKey(' secret ') === 'secret', 'API key should trim');
  assert(
    resolveGstLookupBaseUrl(undefined) === DEFAULT_GST_LOOKUP_BASE_URL,
    'default lookup URL',
  );
  assert(
    resolveGstLookupBaseUrl(' https://example.test/gst/ ') === 'https://example.test/gst',
    'custom lookup URL',
  );
  assert(
    buildGstLookupUrl(DEFAULT_GST_LOOKUP_BASE_URL, '33AAACC1206D1ZN') ===
      'https://www.gstinapi.in/v1/gstin/33AAACC1206D1ZN',
    'gstinapi path URL',
  );

  const gstinApi = mapTaxpayerPayload(
    {
      success: true,
      gstin: '33AAACC1206D1ZN',
      data: {
        gstin: '33AAACC1206D1ZN',
        legal_name: 'CENTRAL WAREHOUSING CORPORATION',
        trade_name: 'CENTRAL WAREHOUSING CORPORATION',
        status: 'Active',
        state_code: '33',
        address: 'No.4, Thiruvalar Illam, North Avenue, Srinagar Colony, Saidapet, Chennai',
        city: 'Saidapet, Chennai',
        address_details: {
          building_number: 'No.4',
          building_name: 'Thiruvalar Illam',
          floor: null,
          street: 'North Avenue',
          locality: 'Srinagar Colony, Saidapet, Chennai',
          pincode: '600015',
        },
        pincode: '600015',
      },
    },
    '33AAACC1206D1ZN',
  );
  assert(gstinApi, 'gstinapi payload should map');
  assert(gstinApi.name === 'CENTRAL WAREHOUSING CORPORATION', `gstinapi name=${gstinApi.name}`);
  assert(gstinApi.doorNo === 'No.4', `gstinapi doorNo=${gstinApi.doorNo}`);
  assert(gstinApi.street.includes('Thiruvalar Illam'), `gstinapi street=${gstinApi.street}`);
  assert(gstinApi.pincode === '600015', `gstinapi pincode=${gstinApi.pincode}`);
  assert(gstinApi.state === 'Tamil Nadu', `gstinapi state=${gstinApi.state}`);
  assert(gstinApi.isActive === true, 'gstinapi active');
  assert(mapTaxpayerPayload({ success: false, message: 'not found' }) === null, 'success false');

  const mapped = mapTaxpayerPayload(sample, '36AABCU9603R1ZM');
  assert(mapped, 'sample payload should map');
  assert(mapped.gstin === '36AABCU9603R1ZM', `gstin=${mapped.gstin}`);
  assert(mapped.name === 'RR Bridal', `name=${mapped.name}`);
  assert(mapped.legalName === 'RR BRIDAL PRIVATE LIMITED', `legalName=${mapped.legalName}`);
  assert(mapped.tradeName === 'RR Bridal', `tradeName=${mapped.tradeName}`);
  assert(mapped.isActive === true, 'active status');
  assert(mapped.doorNo === '12-3', `doorNo=${mapped.doorNo}`);
  assert(mapped.street.includes('Bridal Plaza'), `street=${mapped.street}`);
  assert(mapped.street.includes('Road No 1'), `street=${mapped.street}`);
  assert(mapped.city === 'Madhapur', `city=${mapped.city}`);
  assert(mapped.state === 'Telangana', `state=${mapped.state}`);
  assert(mapped.pincode === '500081', `pincode=${mapped.pincode}`);
  assert(mapped.fullAddress.includes('Madhapur'), `fullAddress=${mapped.fullAddress}`);

  const cancelled = mapTaxpayerPayload({
    lgnm: 'OLD FIRM',
    sts: 'Cancelled',
    gstin: '29AAACR5055K1Z7',
    pradr: { addr: { stcd: 'Karnataka', pncd: '560001', loc: 'Bengaluru' } },
  });
  assert(cancelled, 'cancelled taxpayer should still map');
  assert(cancelled.isActive === false, 'cancelled should not be active');
  assert(cancelled.name === 'OLD FIRM', `cancelled name=${cancelled.name}`);
  assert(cancelled.state === 'Karnataka', `cancelled state=${cancelled.state}`);

  assert(mapTaxpayerPayload({ flag: false, message: 'Invalid GSTIN' }) === null, 'flag false');
  assert(mapTaxpayerPayload({}) === null, 'empty object');

  console.log('gst-lookup.selftest: ok');
}

main();
