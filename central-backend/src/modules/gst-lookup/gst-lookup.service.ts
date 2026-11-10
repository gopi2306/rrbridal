import {
  BadGatewayException,
  Injectable,
  Logger,
  NotFoundException,
} from '@nestjs/common';
import {
  buildGstLookupUrl,
  mapTaxpayerPayload,
  requireApiKey,
  requireValidGstin,
} from './gst-lookup.mapper';
import { GstLookupResult } from './gst-lookup.types';

@Injectable()
export class GstLookupService {
  private readonly logger = new Logger(GstLookupService.name);

  async lookup(gstinRaw: string | undefined): Promise<GstLookupResult> {
    const gstin = requireValidGstin(gstinRaw);
    const apiKey = requireApiKey(process.env.GST_LOOKUP_API_KEY);
    const url = buildGstLookupUrl(process.env.GST_LOOKUP_BASE_URL ?? '', gstin);

    let res: Response;
    try {
      res = await fetch(url, {
        method: 'GET',
        headers: {
          'x-api-key': apiKey,
          Accept: 'application/json',
        },
        signal: AbortSignal.timeout(15_000),
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'GST lookup request failed';
      this.logger.warn(`GST lookup network error for ${gstin}: ${message}`);
      throw new BadGatewayException('Cannot reach the GST lookup service');
    }

    let body: unknown = null;
    try {
      body = await res.json();
    } catch {
      body = null;
    }

    if (!res.ok) {
      const detail = extractProviderMessage(body) || res.statusText;
      this.logger.warn(`GST lookup HTTP ${res.status} for ${gstin}: ${detail}`);
      if (res.status === 404) throw new NotFoundException('GSTIN not found');
      if (res.status === 401) throw new BadGatewayException(detail || 'GST lookup API key is invalid');
      throw new BadGatewayException(detail || 'GST lookup failed');
    }

    if (body && typeof body === 'object' && (body as { success?: unknown }).success === false) {
      throw new NotFoundException(extractProviderMessage(body) || 'GSTIN not found');
    }

    const mapped = mapTaxpayerPayload(body, gstin);
    if (!mapped) throw new NotFoundException('GSTIN not found');
    return mapped;
  }
}

function extractProviderMessage(body: unknown): string {
  if (!body || typeof body !== 'object') return '';
  const root = body as Record<string, unknown>;
  if (typeof root.message === 'string' && root.message.trim()) return root.message.trim();
  if (typeof root.error === 'string' && root.error.trim()) return root.error.trim();
  return '';
}
