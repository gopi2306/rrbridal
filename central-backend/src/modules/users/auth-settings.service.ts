import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { AUTH_SETTINGS_KEY, DEFAULT_ROLE_QUOTAS } from './constants';
import { AuthSettings, AuthSettingsDocument } from './schemas/auth-settings.schema';

@Injectable()
export class AuthSettingsService {
  constructor(@InjectModel(AuthSettings.name) private readonly model: Model<AuthSettingsDocument>) {}

  async ensureDefault() {
    const existing = await this.model.findOne({ settingsKey: AUTH_SETTINGS_KEY }).lean();
    if (existing) return;
    await this.model.create({
      settingsKey: AUTH_SETTINGS_KEY,
      roleQuotas: { ...DEFAULT_ROLE_QUOTAS },
    });
  }

  async getDocument() {
    await this.ensureDefault();
    const doc = await this.model.findOne({ settingsKey: AUTH_SETTINGS_KEY }).lean();
    if (!doc) throw new Error('auth_settings missing after ensureDefault');
    return doc;
  }

  async getQuotaForRole(role: string): Promise<number> {
    const doc = await this.getDocument();
    const raw = doc.roleQuotas[role];
    if (typeof raw === 'number' && raw >= 0) return raw;
    const fallback = DEFAULT_ROLE_QUOTAS[role];
    if (typeof fallback === 'number') return fallback;
    return 99;
  }

  async getQuotas(): Promise<Record<string, number>> {
    const doc = await this.getDocument();
    return { ...DEFAULT_ROLE_QUOTAS, ...doc.roleQuotas };
  }

  async updateRoleQuotas(patch: Record<string, number>) {
    await this.ensureDefault();
    const doc = await this.model.findOne({ settingsKey: AUTH_SETTINGS_KEY });
    if (!doc) throw new Error('auth_settings missing');
    const merged = { ...doc.roleQuotas, ...patch };
    for (const k of Object.keys(merged)) {
      const v = merged[k];
      if (typeof v !== 'number' || v < 0 || !Number.isFinite(v)) {
        delete merged[k];
      }
    }
    doc.roleQuotas = merged;
    await doc.save();
    return doc.toObject();
  }
}
