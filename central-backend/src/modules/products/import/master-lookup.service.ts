import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { isValidObjectIdString, toObjectId } from '../../../common/object-id.util';
import { BatchExpiryDetail, BatchExpiryDetailDocument } from '../../batch-expiry-details/schemas/batch-expiry-detail.schema';
import { BatchSelection, BatchSelectionDocument } from '../../batch-selections/schemas/batch-selection.schema';
import { Brand, BrandDocument } from '../../brands/schemas/brand.schema';
import { Category, CategoryDocument } from '../../categories/schemas/category.schema';
import { CategoriesService } from '../../categories/categories.service';
import { Colour, ColourDocument } from '../../colours/schemas/colour.schema';
import { Department, DepartmentDocument } from '../../departments/schemas/department.schema';
import { GstUom, GstUomDocument } from '../../gst-uoms/schemas/gst-uom.schema';
import { HsnCode, HsnCodeDocument } from '../../hsn-codes/schemas/hsn-code.schema';
import { IndentType, IndentTypeDocument } from '../../indent-types/schemas/indent-type.schema';
import { ItemPrepStatus, ItemPrepStatusDocument } from '../../item-prep-statuses/schemas/item-prep-status.schema';
import { Manufacturer, ManufacturerDocument } from '../../manufacturers/schemas/manufacturer.schema';
import { OfferGroup, OfferGroupDocument } from '../../offer-groups/schemas/offer-group.schema';
import { PackedConfirmation, PackedConfirmationDocument } from '../../packed-confirmations/schemas/packed-confirmation.schema';
import { PoQtyPolicy, PoQtyPolicyDocument } from '../../po-qty-policies/schemas/po-qty-policy.schema';
import { ProductStatus, ProductStatusDocument } from '../../product-statuses/schemas/product-status.schema';
import { SellByType, SellByTypeDocument } from '../../sell-by-types/schemas/sell-by-type.schema';
import { SkuOrderGroup, SkuOrderGroupDocument } from '../../sku-order-groups/schemas/sku-order-group.schema';
import { SkuType, SkuTypeDocument } from '../../sku-types/schemas/sku-type.schema';
import { SubCategoriesService } from '../../sub-categories/sub-categories.service';
import { SubCategory, SubCategoryDocument } from '../../sub-categories/schemas/sub-category.schema';
import { Supplier, SupplierDocument } from '../../suppliers/schemas/supplier.schema';
import { UomSub, UomSubDocument } from '../../uom-subs/schemas/uom-sub.schema';
import { WeightSize, WeightSizeDocument } from '../../weight-sizes/schemas/weight-size.schema';
import { WeightUnit, WeightUnitDocument } from '../../weight-units/schemas/weight-unit.schema';
import type { ParsedProductImportRow } from './product-import.types';

type NameDoc = { _id: Types.ObjectId; name: string };

@Injectable()
export class MasterLookupService {
  private readonly cache = new Map<string, string>();
  private mastersCreated: Record<string, number> = {};

  constructor(
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
    @InjectModel(Department.name) private readonly departmentModel: Model<DepartmentDocument>,
    @InjectModel(Category.name) private readonly categoryModel: Model<CategoryDocument>,
    @InjectModel(SubCategory.name) private readonly subCategoryModel: Model<SubCategoryDocument>,
    @InjectModel(Manufacturer.name) private readonly manufacturerModel: Model<ManufacturerDocument>,
    @InjectModel(Brand.name) private readonly brandModel: Model<BrandDocument>,
    @InjectModel(Colour.name) private readonly colourModel: Model<ColourDocument>,
    @InjectModel(ProductStatus.name) private readonly productStatusModel: Model<ProductStatusDocument>,
    @InjectModel(HsnCode.name) private readonly hsnCodeModel: Model<HsnCodeDocument>,
    @InjectModel(GstUom.name) private readonly gstUomModel: Model<GstUomDocument>,
    @InjectModel(UomSub.name) private readonly uomSubModel: Model<UomSubDocument>,
    @InjectModel(WeightSize.name) private readonly weightSizeModel: Model<WeightSizeDocument>,
    @InjectModel(WeightUnit.name) private readonly weightUnitModel: Model<WeightUnitDocument>,
    @InjectModel(OfferGroup.name) private readonly offerGroupModel: Model<OfferGroupDocument>,
    @InjectModel(SkuType.name) private readonly skuTypeModel: Model<SkuTypeDocument>,
    @InjectModel(SkuOrderGroup.name) private readonly skuOrderGroupModel: Model<SkuOrderGroupDocument>,
    @InjectModel(IndentType.name) private readonly indentTypeModel: Model<IndentTypeDocument>,
    @InjectModel(BatchExpiryDetail.name) private readonly batchExpiryDetailModel: Model<BatchExpiryDetailDocument>,
    @InjectModel(ItemPrepStatus.name) private readonly itemPrepStatusModel: Model<ItemPrepStatusDocument>,
    @InjectModel(PackedConfirmation.name) private readonly packedConfirmationModel: Model<PackedConfirmationDocument>,
    @InjectModel(PoQtyPolicy.name) private readonly poQtyPolicyModel: Model<PoQtyPolicyDocument>,
    @InjectModel(SellByType.name) private readonly sellByTypeModel: Model<SellByTypeDocument>,
    @InjectModel(BatchSelection.name) private readonly batchSelectionModel: Model<BatchSelectionDocument>,
    private readonly categoriesService: CategoriesService,
    private readonly subCategoriesService: SubCategoriesService,
  ) {}

  beginRun(): void {
    this.cache.clear();
    this.mastersCreated = {};
  }

  getMastersCreated(): Record<string, number> {
    return { ...this.mastersCreated };
  }

  private bumpMaster(label: string): void {
    this.mastersCreated[label] = (this.mastersCreated[label] ?? 0) + 1;
  }

  private cacheKey(kind: string, name: string, scope?: string): string {
    return `${kind}::${scope ?? ''}::${name.trim().toLowerCase()}`;
  }

  /** When several masters share a name/code, pick one deterministically (import must not fail). */
  private pickBestMasterMatch<T extends { _id: unknown; isActive?: boolean }>(matches: T[]): T | null {
    if (matches.length === 0) return null;
    const active = matches.filter((m) => m.isActive !== false);
    const pool = active.length > 0 ? active : matches;
    return pool[0] ?? null;
  }

  private looksLikeMasterCode(value: string, prefix: string): boolean {
    const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return new RegExp(`^${escaped}\\d+$`, 'i').test(value.trim());
  }

  private async findByNameExact(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    model: Model<any>,
    name: string,
    extraFilter: Record<string, unknown> = {},
  ): Promise<NameDoc | null> {
    const trimmed = name.trim();
    const escaped = trimmed.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const matches = await model
      .find({
        ...extraFilter,
        name: { $regex: new RegExp(`^${escaped}$`, 'i') },
      })
      .sort({ isActive: -1, _id: 1 })
      .lean();
    return this.pickBestMasterMatch(matches as unknown as Array<NameDoc & { isActive?: boolean }>);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private async findByCodeExact(
    model: Model<any>,
    code: string,
    extraFilter: Record<string, unknown> = {},
  ): Promise<NameDoc | null> {
    const trimmed = code.trim();
    const escaped = trimmed.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const matches = await model
      .find({
        ...extraFilter,
        code: { $regex: new RegExp(`^${escaped}$`, 'i') },
      })
      .sort({ isActive: -1, _id: 1 })
      .lean();
    return this.pickBestMasterMatch(matches as unknown as Array<NameDoc & { isActive?: boolean }>);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private async nextCode(model: Model<any>, prefix: string): Promise<string> {
    const pattern = new RegExp(`^${prefix}\\d+$`, 'i');
    const rows = await model.find({ code: pattern }).select('code').lean();
    let max = 0;
    for (const row of rows) {
      const suffix = row.code.slice(prefix.length);
      const n = parseInt(suffix, 10);
      if (!Number.isNaN(n) && n > max) max = n;
    }
    return `${prefix}${String(max + 1).padStart(3, '0')}`;
  }

  private async resolveCodeMaster(
    label: string,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    model: Model<any>,
    prefix: string,
    name: string | undefined,
    createMissing: boolean,
  ): Promise<string | undefined> {
    if (!name?.trim()) return undefined;
    const key = this.cacheKey(label, name);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const found = await this.findByNameExact(model, name);
    if (found) {
      const id = String(found._id);
      this.cache.set(key, id);
      return id;
    }

    if (this.looksLikeMasterCode(name, prefix)) {
      const byCode = await this.findByCodeExact(model, name);
      if (byCode) {
        const id = String(byCode._id);
        this.cache.set(key, id);
        return id;
      }
    }

    if (!createMissing) throw new Error(`${label} '${name.trim()}' not found`);

    const code = await this.nextCode(model, prefix);
    const created = await model.create({
      code,
      name: name.trim(),
      isActive: true,
    });
    this.bumpMaster(label);
    const id = String(created._id);
    this.cache.set(key, id);
    return id;
  }

  private async resolveWeightAndSizeId(
    row: ParsedProductImportRow,
    createMissing: boolean,
  ): Promise<string | undefined> {
    const direct = MasterLookupService.tryObjectId(row.weightAndSizeId);
    if (direct) return direct;

    const nameOrCode = row.weightSizeName?.trim() || row.weightSizeCode?.trim();
    if (!nameOrCode) return undefined;

    return await this.resolveCodeMaster(
      'WeightSize',
      this.weightSizeModel,
      'ws-',
      nameOrCode,
      createMissing,
    );
  }

  async resolveSupplier(name: string | undefined, createMissing: boolean): Promise<string | undefined> {
    if (!name?.trim()) return undefined;
    const key = this.cacheKey('Supplier', name);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const found = await this.findByNameExact(this.supplierModel, name, {
      isActive: { $ne: false },
    });
    if (found) {
      const id = String(found._id);
      this.cache.set(key, id);
      return id;
    }
    if (!createMissing) throw new Error(`Supplier '${name.trim()}' not found`);

    const created = await this.supplierModel.create({
      name: name.trim(),
      isSupplier: true,
      isActive: true,
    });
    this.bumpMaster('Supplier');
    const id = String(created._id);
    this.cache.set(key, id);
    return id;
  }

  async resolveDepartment(name: string | undefined, createMissing: boolean): Promise<string | undefined> {
    if (!name?.trim()) return undefined;
    const key = this.cacheKey('Department', name);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const found = await this.findByNameExact(this.departmentModel, name, {
      isActive: { $ne: false },
    });
    if (found) {
      const id = String(found._id);
      this.cache.set(key, id);
      return id;
    }
    if (!createMissing) throw new Error(`Department '${name.trim()}' not found`);

    const code = await this.nextCode(this.departmentModel, 'dept-');
    const created = await this.departmentModel.create({
      code,
      name: name.trim(),
      isActive: true,
    });
    this.bumpMaster('Department');
    const id = String(created._id);
    this.cache.set(key, id);
    return id;
  }

  async resolveCategory(
    name: string | undefined,
    departmentId: string | undefined,
    createMissing: boolean,
  ): Promise<string | undefined> {
    if (!name?.trim()) return undefined;
    const scope = departmentId ?? '';
    const key = this.cacheKey('Category', name, scope);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const filter: Record<string, unknown> = { isActive: { $ne: false } };
    if (departmentId) filter.departmentId = departmentId;

    const found = await this.findByNameExact(this.categoryModel, name, filter);
    if (found) {
      const id = String(found._id);
      this.cache.set(key, id);
      return id;
    }
    if (!createMissing) throw new Error(`Category '${name.trim()}' not found`);

    const categoryDto = { name: name.trim(), isActive: true as const };
    const created = await this.categoriesService.create(
      departmentId ? { ...categoryDto, departmentId } : categoryDto,
    );
    this.bumpMaster('Category');
    const id = String(created._id);
    this.cache.set(key, id);
    return id;
  }

  private async resolveHsnCode(
    name: string | undefined,
    gstPercent: number | undefined,
    createMissing: boolean,
  ): Promise<string | undefined> {
    if (!name?.trim()) return undefined;
    const key = this.cacheKey('HsnCode', name);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const trimmed = name.trim();
    const escaped = trimmed.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const regex = new RegExp(`^${escaped}$`, 'i');
    const matches = await this.hsnCodeModel
      .find({
        isActive: { $ne: false },
        $or: [{ name: regex }, { hsnCode: regex }],
      })
      .sort({ _id: 1 })
      .lean();
    const best = this.pickBestMasterMatch(matches as Array<{ _id: unknown; isActive?: boolean }>);
    if (best) {
      const id = String(best._id);
      this.cache.set(key, id);
      return id;
    }
    if (!createMissing) throw new Error(`HsnCode '${trimmed}' not found`);

    const digits = trimmed.replace(/\D/g, '') || trimmed;
    const code = await this.nextCode(this.hsnCodeModel, 'hsn-');
    const created = await this.hsnCodeModel.create({
      code,
      name: trimmed,
      hsnCode: digits,
      gstPercent,
      isActive: true,
    });
    this.bumpMaster('HsnCode');
    const id = String(created._id);
    this.cache.set(key, id);
    return id;
  }

  async resolveSubCategory(
    name: string | undefined,
    categoryId: string | undefined,
    createMissing: boolean,
  ): Promise<string | undefined> {
    if (!name?.trim()) return undefined;
    if (!categoryId) throw new Error(`subCategoryName '${name.trim()}' requires a resolved category`);
    const key = this.cacheKey('SubCategory', name, categoryId);
    const cached = this.cache.get(key);
    if (cached) return cached;

    const found = await this.findByNameExact(this.subCategoryModel, name, {
      categoryId,
      isActive: { $ne: false },
    });
    if (found) {
      const id = String(found._id);
      this.cache.set(key, id);
      return id;
    }
    if (!createMissing) throw new Error(`SubCategory '${name.trim()}' not found`);

    const created = await this.subCategoriesService.create({
      name: name.trim(),
      categoryId,
      isActive: true,
    });
    this.bumpMaster('SubCategory');
    const id = String(created._id);
    this.cache.set(key, id);
    return id;
  }

  /** Resolve all master refs for one import row into CreateProductDto-shaped object ids. */
  async resolveRowToProductRefs(
    row: ParsedProductImportRow,
    createMissing: boolean,
  ): Promise<Record<string, string | undefined>> {
    const departmentId = await this.resolveDepartment(row.departmentName, createMissing);
    const categoryId = await this.resolveCategory(row.categoryName, departmentId, createMissing);
    const supplierNameId = await this.resolveSupplier(row.supplierName, createMissing);

    return {
      supplierNameId,
      departmentId,
      categoryId,
      subCategoryId: await this.resolveSubCategory(row.subCategoryName, categoryId, createMissing),
      manufacturerNameId: await this.resolveCodeMaster(
        'Manufacturer',
        this.manufacturerModel,
        'mfr-',
        row.manufacturerName,
        createMissing,
      ),
      brandId: await this.resolveCodeMaster('Brand', this.brandModel, 'brand-', row.brandName, createMissing),
      colourId: await this.resolveCodeMaster('Colour', this.colourModel, 'clr-', row.colourName, createMissing),
      productStatusId: await this.resolveCodeMaster(
        'ProductStatus',
        this.productStatusModel,
        'ps-',
        row.productStatusName,
        createMissing,
      ),
      hsnCodeId: await this.resolveHsnCode(row.hsnName, row.gstPercent, createMissing),
      gstUomId: await this.resolveCodeMaster('GstUom', this.gstUomModel, 'guom-', row.gstUomName, createMissing),
      uomSubId: await this.resolveCodeMaster('UomSub', this.uomSubModel, 'usub-', row.uomSubName, createMissing),
      weightAndSizeId: await this.resolveWeightAndSizeId(row, createMissing),
      weightPerGmOrMlId: await this.resolveCodeMaster(
        'WeightUnit',
        this.weightUnitModel,
        'wu-',
        row.weightUnitName,
        createMissing,
      ),
      offerGroupId: await this.resolveCodeMaster(
        'OfferGroup',
        this.offerGroupModel,
        'og-',
        row.offerGroupName,
        createMissing,
      ),
      skuTypeId: await this.resolveCodeMaster('SkuType', this.skuTypeModel, 'skt-', row.skuTypeName, createMissing),
      skuOrderGroupId: await this.resolveCodeMaster(
        'SkuOrderGroup',
        this.skuOrderGroupModel,
        'sog-',
        row.skuOrderGroupName,
        createMissing,
      ),
      indentTypeId: await this.resolveCodeMaster(
        'IndentType',
        this.indentTypeModel,
        'it-',
        row.indentTypeName,
        createMissing,
      ),
      batchExpiryDetailId: await this.resolveCodeMaster(
        'BatchExpiryDetail',
        this.batchExpiryDetailModel,
        'bed-',
        row.batchExpiryDetailName,
        createMissing,
      ),
      itemPrepStatusId: await this.resolveCodeMaster(
        'ItemPrepStatus',
        this.itemPrepStatusModel,
        'ips-',
        row.itemPrepStatusName,
        createMissing,
      ),
      packedConfirmationId: await this.resolveCodeMaster(
        'PackedConfirmation',
        this.packedConfirmationModel,
        'pc-',
        row.packedConfirmationName,
        createMissing,
      ),
      poQtyPolicyId: await this.resolveCodeMaster(
        'PoQtyPolicy',
        this.poQtyPolicyModel,
        'pqp-',
        row.poQtyPolicyName,
        createMissing,
      ),
      sellById: await this.resolveCodeMaster('SellByType', this.sellByTypeModel, 'sbt-', row.sellByName, createMissing),
      batchSelectionId: await this.resolveCodeMaster(
        'BatchSelection',
        this.batchSelectionModel,
        'bs-',
        row.batchSelectionName,
        createMissing,
      ),
    };
  }

  static tryObjectId(value: string | undefined): string | undefined {
    if (!value?.trim()) return undefined;
    if (isValidObjectIdString(value)) return toObjectId(value).toString();
    return undefined;
  }
}
