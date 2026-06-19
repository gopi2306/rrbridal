import { ConflictException, Inject, Injectable, NotFoundException, forwardRef } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, SortOrder } from 'mongoose';
import { ResourceLimitsService } from '../resource-limits/resource-limits.service';
import { CreateStoreDto } from './dto/create-store.dto';
import { FilterStoreDto } from './dto/filter-store.dto';
import { UpdateStoreDto } from './dto/update-store.dto';
import { Store, StoreDocument } from './schemas/store.schema';
import type { WhatsAppSettingsDto } from './dto/whatsapp-settings.dto';
import { shouldReplaceAccessToken, toPublicWhatsAppSettings } from '../whatsapp/whatsapp.util';

@Injectable()
export class StoresService {
  constructor(
    @InjectModel(Store.name) private readonly storeModel: Model<StoreDocument>,
    @Inject(forwardRef(() => ResourceLimitsService)) private readonly resourceLimits: ResourceLimitsService,
  ) {}

  async create(dto: CreateStoreDto) {
    await this.resourceLimits.assertStoreLimit();
    const code = dto.code.trim().toLowerCase();
    const existing = await this.storeModel.findOne({ code }).lean();
    if (existing) throw new ConflictException(`Store code '${code}' already exists`);
    return await this.storeModel.create({
      code,
      name: dto.name.trim(),
      address: dto.address?.trim(),
      phone: dto.phone?.trim(),
      status: 'active',
      receiptPrintSettings: dto.receiptPrintSettings,
    });
  }

  async findAll() {
    return await this.storeModel.find().sort({ code: 1 }).lean();
  }

  async findByCode(code: string) {
    const doc = await this.storeModel.findOne({ code: code.trim().toLowerCase() }).lean();
    if (!doc) throw new NotFoundException(`Store '${code}' not found`);
    return doc;
  }

  async update(code: string, dto: UpdateStoreDto) {
    const normalized = code.trim().toLowerCase();
    const prev = await this.storeModel.findOne({ code: normalized }).lean();
    if (!prev) throw new NotFoundException(`Store '${code}' not found`);

    const wasActive = prev.status === 'active';
    const willBeActive = dto.status !== undefined ? dto.status === 'active' : wasActive;
    if (willBeActive && !wasActive) {
      await this.resourceLimits.assertStoreLimitForActivation(normalized);
    }

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.address !== undefined) set.address = dto.address.trim();
    if (dto.phone !== undefined) set.phone = dto.phone.trim();
    if (dto.status !== undefined) set.status = dto.status;
    if (dto.receiptPrintSettings !== undefined) set.receiptPrintSettings = dto.receiptPrintSettings;
    if (Object.keys(set).length === 0) {
      return await this.findByCode(code);
    }
    const doc = await this.storeModel.findOneAndUpdate({ code: normalized }, { $set: set }, { new: true }).lean();
    if (!doc) throw new NotFoundException(`Store '${code}' not found`);
    return doc;
  }

  async removeByCode(code: string) {
    const doc = await this.storeModel
      .findOneAndUpdate(
        { code: code.trim().toLowerCase() },
        { $set: { status: 'inactive' } },
        { new: true },
      )
      .lean();
    if (!doc) throw new NotFoundException(`Store '${code}' not found`);
    return doc;
  }

  async getReceiptPrintSettings(code: string) {
    const doc = await this.findByCode(code);
    return doc.receiptPrintSettings ?? {};
  }

  async getWhatsAppSettingsPublic(code: string) {
    const doc = await this.findByCode(code);
    return toPublicWhatsAppSettings(doc.whatsappSettings as Record<string, unknown> | undefined);
  }

  async updateWhatsAppSettings(code: string, dto: WhatsAppSettingsDto) {
    const normalized = code.trim().toLowerCase();
    const prev = await this.storeModel.findOne({ code: normalized }).lean();
    if (!prev) throw new NotFoundException(`Store '${code}' not found`);

    const current = (prev.whatsappSettings ?? {}) as Record<string, unknown>;
    const next: Record<string, unknown> = { ...current };

    if (dto.enabled !== undefined) next.enabled = dto.enabled;
    if (dto.phoneNumberId !== undefined) next.phoneNumberId = dto.phoneNumberId.trim();
    if (dto.businessAccountId !== undefined) next.businessAccountId = dto.businessAccountId.trim();
    if (dto.templateName !== undefined) next.templateName = dto.templateName.trim();
    if (dto.templateLanguage !== undefined) next.templateLanguage = dto.templateLanguage.trim();
    if (dto.defaultCountryCode !== undefined) next.defaultCountryCode = dto.defaultCountryCode.trim();
    if (dto.attachmentType !== undefined) next.attachmentType = dto.attachmentType;
    if (shouldReplaceAccessToken(dto.accessToken)) next.accessToken = dto.accessToken!.trim();

    const doc = await this.storeModel
      .findOneAndUpdate({ code: normalized }, { $set: { whatsappSettings: next } }, { new: true })
      .lean();
    if (!doc) throw new NotFoundException(`Store '${code}' not found`);
    return toPublicWhatsAppSettings(doc.whatsappSettings as Record<string, unknown> | undefined);
  }

  resolveWhatsAppCredentials(settings: Record<string, unknown> | undefined | null) {
    const s = settings ?? {};
    const phoneNumberId = typeof s.phoneNumberId === 'string' ? s.phoneNumberId.trim() : '';
    const accessToken =
      (typeof s.accessToken === 'string' ? s.accessToken.trim() : '') ||
      process.env.WHATSAPP_DEFAULT_ACCESS_TOKEN?.trim() ||
      '';
    const templateName = typeof s.templateName === 'string' ? s.templateName.trim() : '';
    const templateLanguage = typeof s.templateLanguage === 'string' ? s.templateLanguage.trim() : 'en';
    const defaultCountryCode = typeof s.defaultCountryCode === 'string' ? s.defaultCountryCode.trim() : '91';
    const enabled = Boolean(s.enabled);
    return {
      enabled,
      phoneNumberId,
      accessToken,
      templateName,
      templateLanguage,
      defaultCountryCode,
      attachmentType: typeof s.attachmentType === 'string' ? s.attachmentType.trim() : 'image',
    };
  }

  async existsByCode(code: string): Promise<boolean> {
    const count = await this.storeModel.countDocuments({ code: code.trim().toLowerCase() });
    return count > 0;
  }

  async filter(dto: FilterStoreDto) {
    const filter: FilterQuery<StoreDocument> = {};

    if (dto.code) filter.code = dto.code.trim().toLowerCase();
    if (dto.name) filter.name = dto.name;
    if (dto.phone) filter.phone = dto.phone;
    if (dto.status) filter.status = dto.status;

    if (dto.search) {
      filter.$or = [
        { code: { $regex: dto.search, $options: 'i' } },
        { name: { $regex: dto.search, $options: 'i' } },
        { address: { $regex: dto.search, $options: 'i' } },
        { phone: { $regex: dto.search, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy ?? 'code';
    const sortOrder: SortOrder = dto.sortOrder === 'desc' ? -1 : 1;

    const [data, total] = await Promise.all([
      this.storeModel
        .find(filter)
        .sort({ [sortBy]: sortOrder })
        .skip(skip)
        .limit(limit)
        .lean(),
      this.storeModel.countDocuments(filter),
    ]);

    return {
      data,
      total,
      page,
      limit,
      totalPages: Math.ceil(total / limit),
    };
  }
}
