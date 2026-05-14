import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { FilterQuery, Model, PipelineStage, SortOrder, Types } from 'mongoose';
import { Branch, BranchDocument } from '../branches/schemas/branch.schema';
import { Division, DivisionDocument } from '../divisions/schemas/division.schema';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import { Supplier, SupplierDocument } from '../suppliers/schemas/supplier.schema';
import { CreatePurchaseReturnDto } from './dto/create-purchase-return.dto';
import { FilterPurchaseReturnDto } from './dto/filter-purchase-return.dto';
import { UpdatePurchaseReturnDto } from './dto/update-purchase-return.dto';
import { PurchaseReturn, PurchaseReturnDocument } from './schemas/purchase-return.schema';

const PR_SORT_FIELDS = new Set([
  'updatedAt',
  'createdAt',
  'purchaseReturnNo',
  'purchaseReturnDate',
  'netAmount',
  'branchId',
  'mainDivisionId',
  'mainLocationId',
  'pucOutSlipNo',
  'itemDiscAmount',
  'cashDiscAmount',
]);

@Injectable()
export class PurchaseReturnsService {
  constructor(
    @InjectModel(PurchaseReturn.name) private readonly prModel: Model<PurchaseReturnDocument>,
    @InjectModel(Branch.name) private readonly branchModel: Model<BranchDocument>,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
    @InjectModel(Division.name) private readonly divisionModel: Model<DivisionDocument>,
    @InjectModel(Supplier.name) private readonly supplierModel: Model<SupplierDocument>,
  ) {}

  private async nextPrNo() {
    const suffix = Math.floor(1000 + Math.random() * 9000);
    return `PR-${suffix}`;
  }

  /** Mongo ObjectId hex (24 chars); also what seed stores on branchId / location / division refs. */
  private is24HexObjectId(s: string): boolean {
    return /^[a-fA-F0-9]{24}$/.test(s);
  }

  private escapeRegex(s: string): string {
    return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  /** Resolve branch/location/division filter: accept either ObjectId string or master `code`. */
  private async resolveCodeOrId(model: Model<any>, value: string | undefined): Promise<string | undefined> {
    if (value === undefined || value === null) return undefined;
    const trimmed = String(value).trim();
    if (trimmed === '') return undefined;
    if (this.is24HexObjectId(trimmed)) return trimmed;
    const found = (await model.findOne({ code: trimmed.toLowerCase() }).select('_id').lean()) as { _id: unknown } | null;
    return found ? String(found._id) : trimmed;
  }

  private lookupByRefField(fromCollection: string, localField: string, asKey: string): PipelineStage[] {
    const tmp = `_lk_${asKey}`;
    return [
      {
        $lookup: {
          from: fromCollection,
          let: { ref: { $toString: { $ifNull: [`$${localField}`, ''] } } },
          pipeline: [
            {
              $match: {
                $expr: {
                  $or: [
                    {
                      $and: [
                        { $ne: ['$$ref', ''] },
                        {
                          $eq: [
                            '$_id',
                            { $convert: { input: '$$ref', to: 'objectId', onError: null, onNull: null } },
                          ],
                        },
                      ],
                    },
                    {
                      $and: [
                        { $ne: ['$$ref', ''] },
                        { $eq: ['$code', { $toLower: '$$ref' }] },
                      ],
                    },
                  ],
                },
              },
            },
            {
              $project: {
                _id: 1,
                code: 1,
                name: 1,
                isActive: 1,
                address: 1,
                phone: 1,
                type: 1,
                description: 1,
              },
            },
          ],
          as: tmp,
        },
      },
      {
        $addFields: {
          [asKey]: { $cond: [{ $gt: [{ $size: `$${tmp}` }, 0] }, { $arrayElemAt: [`$${tmp}`, 0] }, null] },
        },
      },
      { $project: { [tmp]: 0 } },
    ];
  }

  private lookupSupplierMaster(fromCollection: string): PipelineStage[] {
    const tmp = '_lk_supplierMaster';
    return [
      {
        $lookup: {
          from: fromCollection,
          let: { ref: { $toString: { $ifNull: ['$supplier.supplierId', ''] } } },
          pipeline: [
            {
              $match: {
                $expr: {
                  $and: [
                    { $ne: ['$$ref', ''] },
                    {
                      $eq: [
                        '$_id',
                        { $convert: { input: '$$ref', to: 'objectId', onError: null, onNull: null } },
                      ],
                    },
                  ],
                },
              },
            },
            {
              $project: {
                _id: 1,
                name: 1,
                gstNumber: 1,
                emailId: 1,
                mobileNo: 1,
                isActive: 1,
              },
            },
          ],
          as: tmp,
        },
      },
      {
        $addFields: {
          supplierMaster: { $cond: [{ $gt: [{ $size: `$${tmp}` }, 0] }, { $arrayElemAt: [`$${tmp}`, 0] }, null] },
        },
      },
      { $project: { [tmp]: 0 } },
    ];
  }

  /** Branch, division, location, and supplier master documents for API responses. */
  private enrichLookupStages(): PipelineStage[] {
    return [
      ...this.lookupByRefField(this.branchModel.collection.name, 'branchId', 'branch'),
      ...this.lookupByRefField(this.divisionModel.collection.name, 'mainDivisionId', 'mainDivision'),
      ...this.lookupByRefField(this.locationModel.collection.name, 'mainLocationId', 'mainLocation'),
      ...this.lookupSupplierMaster(this.supplierModel.collection.name),
    ];
  }

  private async oneEnrichedById(id: string) {
    if (!this.is24HexObjectId(id)) {
      throw new NotFoundException('Purchase return not found');
    }
    const rows = await this.prModel.aggregate([
      { $match: { _id: new Types.ObjectId(id) } },
      ...this.enrichLookupStages(),
    ]);
    const doc = rows[0];
    if (!doc) throw new NotFoundException('Purchase return not found');
    return doc;
  }

  async create(dto: CreatePurchaseReturnDto) {
    let purchaseReturnNo = dto.purchaseReturnNo?.trim();
    if (purchaseReturnNo) {
      const clash = await this.prModel.exists({ purchaseReturnNo });
      if (clash) throw new ConflictException(`Purchase return number '${purchaseReturnNo}' already exists`);
    } else {
      purchaseReturnNo = await this.nextPrNo();
    }
    const created = await this.prModel.create({
      purchaseReturnNo,
      branchId: dto.branchId,
      mainDivisionId: dto.mainDivisionId,
      mainLocationId: dto.mainLocationId,
      supplier: dto.supplier,
      purchaseReturnDate: dto.purchaseReturnDate,
      pucOutSlipNo: dto.pucOutSlipNo,
      itemDiscAmount: dto.itemDiscAmount,
      cashDiscAmount: dto.cashDiscAmount,
      netAmount: dto.netAmount,
      lines: dto.lines ?? [],
    });
    return await this.oneEnrichedById(String(created._id));
  }

  async findById(id: string) {
    return await this.oneEnrichedById(id);
  }

  async update(id: string, dto: UpdatePurchaseReturnDto) {
    if (!this.is24HexObjectId(id)) throw new NotFoundException('Purchase return not found');
    if (dto.purchaseReturnNo?.trim()) {
      const clash = await this.prModel.findOne({
        purchaseReturnNo: dto.purchaseReturnNo.trim(),
        _id: { $ne: new Types.ObjectId(id) },
      }).lean();
      if (clash) throw new ConflictException(`Purchase return number '${dto.purchaseReturnNo.trim()}' already exists`);
    }
    const doc = await this.prModel.findByIdAndUpdate(id, { $set: dto }, { new: true }).lean();
    if (!doc) throw new NotFoundException('Purchase return not found');
    return await this.oneEnrichedById(id);
  }

  async list(params: { search?: string; supplierId?: string }) {
    const filter: Record<string, unknown> = {};
    if (params.supplierId) filter['supplier.supplierId'] = params.supplierId;
    if (params.search) {
      const safe = this.escapeRegex(params.search);
      filter.$or = [
        { purchaseReturnNo: { $regex: safe, $options: 'i' } },
        { 'supplier.name': { $regex: safe, $options: 'i' } },
      ];
    }
    return await this.prModel.aggregate([
      { $match: filter },
      { $sort: { updatedAt: -1 } },
      { $limit: 200 },
      ...this.enrichLookupStages(),
    ]);
  }

  async filter(dto: FilterPurchaseReturnDto) {
    const [resBranchId, resMainDivisionId, resMainLocationId] = await Promise.all([
      this.resolveCodeOrId(this.branchModel, dto.branchId),
      this.resolveCodeOrId(this.divisionModel, dto.mainDivisionId),
      this.resolveCodeOrId(this.locationModel, dto.mainLocationId),
    ]);

    const filter: FilterQuery<PurchaseReturnDocument> = {};

    if (dto.purchaseReturnNo?.trim()) {
      const safe = this.escapeRegex(dto.purchaseReturnNo.trim());
      filter.purchaseReturnNo = new RegExp(`^${safe}$`, 'i');
    }
    if (dto.supplierId?.trim()) filter['supplier.supplierId'] = dto.supplierId.trim();
    if (resBranchId !== undefined) filter.branchId = resBranchId;
    if (resMainDivisionId !== undefined) filter.mainDivisionId = resMainDivisionId;
    if (resMainLocationId !== undefined) filter.mainLocationId = resMainLocationId;
    if (dto.pucOutSlipNo) filter.pucOutSlipNo = dto.pucOutSlipNo;

    if (dto.purchaseReturnDateFrom || dto.purchaseReturnDateTo) {
      filter.purchaseReturnDate = {};
      if (dto.purchaseReturnDateFrom) filter.purchaseReturnDate.$gte = dto.purchaseReturnDateFrom;
      if (dto.purchaseReturnDateTo) filter.purchaseReturnDate.$lte = dto.purchaseReturnDateTo;
    }

    if (dto.netAmountMin !== undefined || dto.netAmountMax !== undefined) {
      filter.netAmount = {};
      if (dto.netAmountMin !== undefined) filter.netAmount.$gte = dto.netAmountMin;
      if (dto.netAmountMax !== undefined) filter.netAmount.$lte = dto.netAmountMax;
    }

    if (dto.search) {
      const safe = this.escapeRegex(dto.search);
      filter.$or = [
        { purchaseReturnNo: { $regex: safe, $options: 'i' } },
        { 'supplier.name': { $regex: safe, $options: 'i' } },
        { pucOutSlipNo: { $regex: safe, $options: 'i' } },
      ];
    }

    const page = dto.page ?? 1;
    const limit = dto.limit ?? 20;
    const skip = (page - 1) * limit;
    const sortBy = dto.sortBy && PR_SORT_FIELDS.has(dto.sortBy) ? dto.sortBy : 'updatedAt';
    const sortOrder: SortOrder = dto.sortOrder === 'asc' ? 1 : -1;

    const [data, total] = await Promise.all([
      this.prModel.aggregate([
        { $match: filter as Record<string, unknown> },
        { $sort: { [sortBy]: sortOrder } as Record<string, 1 | -1> },
        { $skip: skip },
        { $limit: limit },
        ...this.enrichLookupStages(),
      ]),
      this.prModel.countDocuments(filter),
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
