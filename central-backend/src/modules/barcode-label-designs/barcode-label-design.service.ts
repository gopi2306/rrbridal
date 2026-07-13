import {
  BadRequestException,
  Injectable,
  NotFoundException,
  OnModuleInit,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import {
  normalizeCreateBarcodeLabelDesignDto,
  normalizeUpdateBarcodeLabelDesignDto,
  serializeStyles,
  stylesRecordFromInput,
} from './barcode-label-design-payload.util';
import {
  BARCODE_DECORATIONS,
  BARCODE_LAYOUT_STYLES,
} from './barcode-label-design.types';
import {
  BARCODE_PRINTER_PROFILE_SEEDS,
  DEFAULT_RETAIL_STACKED_DESIGN,
  DEFAULT_RETAIL_STACKED_FIELD_STYLES,
} from './barcode-label-design.seed';
import { CreateBarcodeLabelDesignDto } from './dto/create-barcode-label-design.dto';
import { UpdateBarcodeLabelDesignDto } from './dto/update-barcode-label-design.dto';
import {
  BarcodeLabelDesign,
  BarcodeLabelDesignDocument,
} from './schemas/barcode-label-design.schema';
import {
  BarcodePrinterProfile,
  BarcodePrinterProfileDocument,
} from './schemas/barcode-printer-profile.schema';

function serializeDesign(doc: Record<string, unknown>) {
  return {
    ...doc,
    styles: serializeStyles(doc.styles as Record<string, unknown>),
  };
}

@Injectable()
export class BarcodeLabelDesignService implements OnModuleInit {
  constructor(
    @InjectModel(BarcodeLabelDesign.name)
    private readonly designModel: Model<BarcodeLabelDesignDocument>,
    @InjectModel(BarcodePrinterProfile.name)
    private readonly profileModel: Model<BarcodePrinterProfileDocument>,
  ) {}

  async onModuleInit() {
    await this.ensureSeeds();
  }

  async ensureSeeds() {
    for (const profile of BARCODE_PRINTER_PROFILE_SEEDS) {
      await this.profileModel.updateOne(
        { profileId: profile.profileId },
        { $setOnInsert: { ...profile } },
        { upsert: true },
      );
    }

    const activeCount = await this.designModel.countDocuments({ isActive: true });
    if (activeCount === 0) {
      await this.designModel.create({
        ...DEFAULT_RETAIL_STACKED_DESIGN,
        styles: stylesRecordFromInput(DEFAULT_RETAIL_STACKED_FIELD_STYLES),
      });
    }
  }

  async listPrinterProfiles() {
    await this.ensureSeeds();
    return await this.profileModel.find().sort({ name: 1 }).lean();
  }

  async listDesigns() {
    await this.ensureSeeds();
    const rows = await this.designModel.find().sort({ isActive: -1, updatedAt: -1 }).lean();
    return rows.map((row) => serializeDesign(row as Record<string, unknown>));
  }

  async getActiveDesign() {
    await this.ensureSeeds();
    const doc = await this.designModel.findOne({ isActive: true }).sort({ updatedAt: -1 }).lean();
    if (!doc) {
      throw new NotFoundException('No active barcode label design configured');
    }
    const profile = await this.profileModel
      .findOne({ profileId: doc.printerProfileId })
      .lean();
    return {
      design: serializeDesign(doc as Record<string, unknown>),
      printerProfile: profile ?? null,
    };
  }

  async create(dto: CreateBarcodeLabelDesignDto) {
    await this.ensureSeeds();
    const normalized = normalizeCreateBarcodeLabelDesignDto(dto);
    const profile = await this.requireProfile(normalized.printerProfileId);
    const payload = this.buildDesignPayload(normalized, profile);
    const created = await this.designModel.create(payload);
    if (dto.activate) {
      await this.activate(String(created._id));
      return serializeDesign(
        (await this.designModel.findById(created._id).lean()) as Record<string, unknown>,
      );
    }
    return serializeDesign(created.toObject() as unknown as Record<string, unknown>);
  }

  async update(id: string, dto: UpdateBarcodeLabelDesignDto) {
    this.assertObjectId(id);
    await this.ensureSeeds();
    const normalized = normalizeUpdateBarcodeLabelDesignDto(dto);
    const existing = await this.designModel.findById(id).lean();
    if (!existing) throw new NotFoundException(`Barcode label design '${id}' not found`);

    const profileId = normalized.printerProfileId ?? existing.printerProfileId;
    const profile = await this.requireProfile(profileId);
    const merged = this.mergeDesignUpdate(existing, normalized, profile);
    const updateDoc: Record<string, unknown> = { ...merged };
    if (dto.customBrandText === '') {
      delete updateDoc.customBrandText;
      await this.designModel.findByIdAndUpdate(id, {
        $set: updateDoc,
        $unset: { customBrandText: '' },
      });
    } else {
      await this.designModel.findByIdAndUpdate(id, { $set: updateDoc });
    }
    const updated = await this.designModel.findById(id).lean();
    if (!updated) throw new NotFoundException(`Barcode label design '${id}' not found`);

    if (dto.activate) {
      await this.activate(id);
      return serializeDesign(
        (await this.designModel.findById(id).lean()) as Record<string, unknown>,
      );
    }

    return serializeDesign(updated as Record<string, unknown>);
  }

  async remove(id: string) {
    this.assertObjectId(id);
    const existing = await this.designModel.findById(id).lean();
    if (!existing) throw new NotFoundException(`Barcode label design '${id}' not found`);
    if (existing.isActive) {
      throw new BadRequestException('Cannot delete the active barcode label design');
    }
    await this.designModel.deleteOne({ _id: id });
    return { deleted: true };
  }

  async activate(id: string) {
    this.assertObjectId(id);
    const existing = await this.designModel.findById(id).lean();
    if (!existing) throw new NotFoundException(`Barcode label design '${id}' not found`);

    await this.designModel.updateMany({ isActive: true }, { $set: { isActive: false } });
    const updated = await this.designModel
      .findByIdAndUpdate(id, { $set: { isActive: true } }, { new: true })
      .lean();
    return serializeDesign(updated as Record<string, unknown>);
  }

  private async requireProfile(profileId: string) {
    const profile = await this.profileModel.findOne({ profileId: profileId.trim() }).lean();
    if (!profile) {
      throw new BadRequestException(`Unknown printer profile '${profileId}'`);
    }
    return profile;
  }

  private buildDesignPayload(
    dto: CreateBarcodeLabelDesignDto,
    profile: { labelWidthMm: number; labelHeightMm: number; labelsPerRow: number; dpi: number },
  ) {
    const layoutStyle = dto.layoutStyle;
    if (!BARCODE_LAYOUT_STYLES.includes(layoutStyle as (typeof BARCODE_LAYOUT_STYLES)[number])) {
      throw new BadRequestException(`Invalid layoutStyle '${layoutStyle}'`);
    }

    return {
      name: dto.name.trim(),
      isActive: false,
      layoutStyle,
      printerProfileId: dto.printerProfileId.trim(),
      labelWidthMm: dto.labelWidthMm ?? profile.labelWidthMm,
      labelHeightMm: dto.labelHeightMm ?? profile.labelHeightMm,
      labelsPerRow: dto.labelsPerRow ?? profile.labelsPerRow,
      dpi: dto.dpi ?? profile.dpi,
      fields: {
        ...DEFAULT_RETAIL_STACKED_DESIGN.fields,
        ...(dto.fields ?? {}),
      },
      text: {
        ...DEFAULT_RETAIL_STACKED_DESIGN.text,
        ...(dto.text ?? {}),
      },
      barcode: dto.barcode,
      styles: stylesRecordFromInput(dto.styles as Record<string, unknown> | undefined),
      decoration:
        dto.decoration && BARCODE_DECORATIONS.includes(dto.decoration as (typeof BARCODE_DECORATIONS)[number])
          ? dto.decoration
          : 'none',
      printOffsetMm: {
        vertical: dto.printOffsetMm?.vertical ?? 0,
        horizontal: dto.printOffsetMm?.horizontal ?? 0,
      },
      ...(dto.customBrandText !== undefined && dto.customBrandText !== ''
        ? { customBrandText: dto.customBrandText.trim() }
        : {}),
    };
  }

  private mergeDesignUpdate(
    existing: Record<string, unknown>,
    dto: UpdateBarcodeLabelDesignDto,
    profile: { labelWidthMm: number; labelHeightMm: number; labelsPerRow: number; dpi: number },
  ) {
    const existingFields = (existing.fields ?? {}) as Record<string, boolean>;
    const existingText = (existing.text ?? {}) as Record<string, string>;
    const existingBarcode = (existing.barcode ?? {}) as { heightMm: number; widthMm: number };
    const existingOffset = (existing.printOffsetMm ?? {}) as { vertical: number; horizontal: number };

    const layoutStyle = dto.layoutStyle ?? existing.layoutStyle;
    if (!BARCODE_LAYOUT_STYLES.includes(layoutStyle as (typeof BARCODE_LAYOUT_STYLES)[number])) {
      throw new BadRequestException(`Invalid layoutStyle '${layoutStyle}'`);
    }

    const mergedStyles = stylesRecordFromInput({
      ...serializeStyles(existing.styles as Record<string, unknown>),
      ...(dto.styles as Record<string, unknown> | undefined),
    });

    return {
      ...(dto.name !== undefined ? { name: dto.name.trim() } : {}),
      layoutStyle,
      printerProfileId: dto.printerProfileId?.trim() ?? existing.printerProfileId,
      labelWidthMm: dto.labelWidthMm ?? existing.labelWidthMm ?? profile.labelWidthMm,
      labelHeightMm: dto.labelHeightMm ?? existing.labelHeightMm ?? profile.labelHeightMm,
      labelsPerRow: dto.labelsPerRow ?? existing.labelsPerRow ?? profile.labelsPerRow,
      dpi: dto.dpi ?? existing.dpi ?? profile.dpi,
      fields: { ...existingFields, ...(dto.fields ?? {}) },
      text: { ...existingText, ...(dto.text ?? {}) },
      barcode: dto.barcode ?? existingBarcode,
      styles: mergedStyles,
      decoration:
        dto.decoration ??
        existing.decoration ??
        'none',
      printOffsetMm: {
        vertical: dto.printOffsetMm?.vertical ?? existingOffset.vertical ?? 0,
        horizontal: dto.printOffsetMm?.horizontal ?? existingOffset.horizontal ?? 0,
      },
      ...(dto.customBrandText !== undefined && dto.customBrandText !== ''
        ? { customBrandText: dto.customBrandText.trim() }
        : {}),
    };
  }

  private assertObjectId(id: string) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid design id');
    }
  }
}
