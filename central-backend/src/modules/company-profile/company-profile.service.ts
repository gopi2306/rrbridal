import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { PatchCompanyProfileDto } from './dto/patch-company-profile.dto';
import { CompanyProfile, CompanyProfileDocument, COMPANY_PROFILE_KEY } from './schemas/company-profile.schema';

@Injectable()
export class CompanyProfileService {
  constructor(@InjectModel(CompanyProfile.name) private readonly model: Model<CompanyProfileDocument>) {}

  private trimOrUndef(s: string | undefined): string | undefined {
    if (s === undefined) return undefined;
    const t = s.trim();
    return t === '' ? undefined : t;
  }

  async get() {
    let doc = await this.model.findOne({ settingsKey: COMPANY_PROFILE_KEY }).lean();
    if (!doc) {
      await this.model.create({ settingsKey: COMPANY_PROFILE_KEY });
      doc = await this.model.findOne({ settingsKey: COMPANY_PROFILE_KEY }).lean();
    }
    return doc!;
  }

  async patch(dto: PatchCompanyProfileDto) {
    const set: Record<string, unknown> = {};
    if (dto.legalName !== undefined) set.legalName = this.trimOrUndef(dto.legalName);
    if (dto.tradeName !== undefined) set.tradeName = this.trimOrUndef(dto.tradeName);
    if (dto.gstin !== undefined) set.gstin = this.trimOrUndef(dto.gstin);
    if (dto.address !== undefined) set.address = this.trimOrUndef(dto.address);
    if (dto.city !== undefined) set.city = this.trimOrUndef(dto.city);
    if (dto.state !== undefined) set.state = this.trimOrUndef(dto.state);
    if (dto.pinCode !== undefined) set.pinCode = this.trimOrUndef(dto.pinCode);
    if (dto.phone !== undefined) set.phone = this.trimOrUndef(dto.phone);
    if (dto.email !== undefined) set.email = this.trimOrUndef(dto.email);
    if (dto.companyLogo !== undefined) set.companyLogo = this.trimOrUndef(dto.companyLogo);
    if (dto.fssaiNo !== undefined) set.fssaiNo = this.trimOrUndef(dto.fssaiNo);
    if (dto.website !== undefined) set.website = this.trimOrUndef(dto.website);
    if (dto.termsAndConditions !== undefined) set.termsAndConditions = this.trimOrUndef(dto.termsAndConditions);
    if (dto.thankYouLine !== undefined) set.thankYouLine = this.trimOrUndef(dto.thankYouLine);
    if (dto.policyLines !== undefined) set.policyLines = dto.policyLines;
    if (dto.receiptQrSlots !== undefined) set.receiptQrSlots = dto.receiptQrSlots.slice(0, 3);
    if (dto.receiptBarcodeEnabled !== undefined) set.receiptBarcodeEnabled = dto.receiptBarcodeEnabled;
    if (dto.extraFields !== undefined) set.extraFields = dto.extraFields;

    if (Object.keys(set).length === 0) {
      return await this.get();
    }

    await this.model.findOneAndUpdate({ settingsKey: COMPANY_PROFILE_KEY }, { $set: set }, { upsert: true, new: true });
    return await this.get();
  }
}
