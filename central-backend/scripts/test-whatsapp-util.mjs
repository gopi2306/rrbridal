import {
  buildTemplateBodyParams,
  maskAccessToken,
  normalizeWhatsAppPhone,
  toPublicWhatsAppSettings,
} from '../dist/modules/whatsapp/whatsapp.util.js';

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

assert(maskAccessToken('abcdefgh') === '****efgh', 'mask token');
assert(normalizeWhatsAppPhone('9876543210', '91') === '919876543210', 'e164');
assert(
  buildTemplateBodyParams({
    customerName: 'A',
    storeName: 'B',
    billNo: 'INV-1',
    amountLabel: '₹10.00',
  }).length === 4,
  'template params',
);

const pub = toPublicWhatsAppSettings({
  enabled: true,
  phoneNumberId: '123',
  templateName: 'invoice_delivery',
  accessToken: 'secret-token-xyz',
});
assert(pub.configured === false || pub.configured === true, 'public settings');
assert(pub.accessTokenMasked === '****-xyz', 'masked in public');

console.log('whatsapp util tests: ok');
