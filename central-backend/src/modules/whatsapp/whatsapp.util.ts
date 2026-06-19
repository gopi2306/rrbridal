export function maskAccessToken(token?: string): string | undefined {
  if (!token?.trim()) return undefined;
  const t = token.trim();
  if (t.length <= 4) return '****';
  return `****${t.slice(-4)}`;
}

export function shouldReplaceAccessToken(incoming?: string): boolean {
  if (incoming === undefined || incoming === null) return false;
  const t = incoming.trim();
  if (!t) return false;
  if (t.startsWith('****')) return false;
  return true;
}

export function normalizeWhatsAppPhone(phone: string, defaultCountryCode = '91'): string {
  const digits = phone.replace(/\D/g, '');
  if (!digits) return '';
  if (digits.length === 10) return `${defaultCountryCode.replace(/\D/g, '')}${digits}`;
  if (digits.length === 12 && digits.startsWith('91')) return digits;
  if (digits.length > 10 && digits.startsWith(defaultCountryCode.replace(/\D/g, ''))) return digits;
  return digits;
}

export function buildTemplateBodyParams(input: {
  customerName: string;
  storeName: string;
  billNo: string;
  amountLabel: string;
}): string[] {
  return [
    input.customerName.trim() || 'Customer',
    input.storeName.trim() || 'Store',
    input.billNo.trim(),
    input.amountLabel.trim(),
  ];
}

export type WhatsAppSettingsPublic = {
  enabled: boolean;
  configured: boolean;
  phoneNumberId?: string;
  businessAccountId?: string;
  accessTokenMasked?: string;
  templateName?: string;
  templateLanguage?: string;
  defaultCountryCode: string;
  attachmentType: string;
};

export function toPublicWhatsAppSettings(
  settings: Record<string, unknown> | undefined | null,
): WhatsAppSettingsPublic {
  const s = settings ?? {};
  const phoneNumberId = readString(s.phoneNumberId);
  const templateName = readString(s.templateName);
  const accessToken = readString(s.accessToken);
  const envToken = process.env.WHATSAPP_DEFAULT_ACCESS_TOKEN?.trim();
  const effectiveToken = accessToken || envToken || '';
  const configured = Boolean(phoneNumberId && effectiveToken && templateName);
  const result: WhatsAppSettingsPublic = {
    enabled: Boolean(s.enabled),
    configured,
    templateLanguage: readString(s.templateLanguage) || 'en',
    defaultCountryCode: readString(s.defaultCountryCode) || '91',
    attachmentType: readString(s.attachmentType) || 'image',
  };
  if (phoneNumberId) result.phoneNumberId = phoneNumberId;
  const businessAccountId = readString(s.businessAccountId);
  if (businessAccountId) result.businessAccountId = businessAccountId;
  if (effectiveToken) {
    const masked = maskAccessToken(effectiveToken);
    if (masked) result.accessTokenMasked = masked;
  }
  if (templateName) result.templateName = templateName;
  return result;
}

function readString(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined;
  const t = value.trim();
  return t || undefined;
}
