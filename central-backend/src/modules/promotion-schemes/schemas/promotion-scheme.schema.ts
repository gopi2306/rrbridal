import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type PromotionSchemeDocument = HydratedDocument<PromotionScheme>;

export const PROMOTION_SCHEME_KINDS = ['scheme', 'offer'] as const;
export type PromotionSchemeKind = (typeof PROMOTION_SCHEME_KINDS)[number];

export const PROMOTION_SCHEME_TYPES = ['item', 'bill', 'combo', 'slab'] as const;
export type PromotionSchemeType = (typeof PROMOTION_SCHEME_TYPES)[number];

export const PROMOTION_STACKING_MODES = ['best_benefit', 'highest_priority', 'allow_stack'] as const;
export type PromotionStackingMode = (typeof PROMOTION_STACKING_MODES)[number];

/** Item-type benefit modes (`type === 'item'`). */
export const PROMOTION_ITEM_BENEFIT_MODES = ['buy_x_get_y', 'percent_off', 'flat_off'] as const;
export type PromotionItemBenefitMode = (typeof PROMOTION_ITEM_BENEFIT_MODES)[number];

/** Which unit is free for buy-x-get-y. */
export const PROMOTION_FREE_ON = ['cheapest', 'highest'] as const;
export type PromotionFreeOn = (typeof PROMOTION_FREE_ON)[number];

@Schema({ _id: false })
export class PromotionTimeWindow {
  @Prop({ required: true, min: 0, max: 6 })
  dayOfWeek!: number;

  @Prop({ required: true, min: 0, max: 23 })
  fromHour!: number;

  @Prop({ required: true, min: 0, max: 23 })
  toHour!: number;
}

@Schema({ _id: false })
export class PromotionComboRequirement {
  @Prop({ required: true, trim: true })
  sku!: string;

  @Prop({ required: true, min: 1 })
  requiredQty!: number;
}

@Schema({ _id: false })
export class PromotionConditions {
  @Prop({ type: [String], default: [] })
  skus!: string[];

  @Prop({ type: [String], default: [] })
  categoryIds!: string[];

  @Prop({ type: [String], default: [] })
  brandIds!: string[];

  @Prop({ type: [String], default: [] })
  offerGroupIds!: string[];

  @Prop({ min: 0 })
  minLineQty?: number;

  @Prop({ min: 0 })
  minBillAmount?: number;

  @Prop({ type: [String], default: [] })
  customerTypes!: string[];

  @Prop({ type: [String], default: [] })
  customerCodes!: string[];

  @Prop({ type: [PromotionComboRequirement], default: [] })
  requiredSkus!: PromotionComboRequirement[];
}

@Schema({ _id: false })
export class PromotionSlab {
  @Prop({ required: true, min: 0 })
  fromAmount!: number;

  @Prop({ min: 0 })
  toAmount?: number;

  @Prop({ min: 0, max: 100 })
  discountPercent!: number;
}

/**
 * Discount payload — shape depends on parent `type`:
 * - item:  mode, buyQty/getQty/freeOn (BXGY) or discountPercent/flatAmount
 * - bill:  discountPercent and/or flatAmount, optional minBillAmount
 * - slab:  slabs[]
 * - combo: comboSkus[], fixedPrice
 */
@Schema({ _id: false })
export class PromotionBenefit {
  /** Item schemes only. */
  @Prop({ enum: PROMOTION_ITEM_BENEFIT_MODES })
  mode?: PromotionItemBenefitMode;

  @Prop({ min: 1 })
  buyQty?: number;

  @Prop({ min: 1 })
  getQty?: number;

  @Prop({ enum: PROMOTION_FREE_ON, default: 'cheapest' })
  freeOn?: PromotionFreeOn;

  @Prop({ min: 0, max: 100 })
  discountPercent?: number;

  @Prop({ min: 0 })
  flatAmount?: number;

  /** Bill / slab threshold (₹, 4 dp). */
  @Prop({ min: 0 })
  minBillAmount?: number;

  /** Slab schemes only. */
  @Prop({ type: [PromotionSlab], default: [] })
  slabs!: PromotionSlab[];

  /** Combo schemes only. */
  @Prop({ type: [String], default: [] })
  comboSkus!: string[];

  @Prop({ min: 0 })
  fixedPrice?: number;
}

@Schema({ timestamps: true, collection: 'promotion_schemes' })
export class PromotionScheme {
  @Prop({ required: true, trim: true, lowercase: true, unique: true, index: true })
  code!: string;

  @Prop({ required: true, trim: true })
  name!: string;

  @Prop({ trim: true })
  description?: string;

  @Prop({ required: true, enum: PROMOTION_SCHEME_KINDS, default: 'scheme' })
  kind!: PromotionSchemeKind;

  @Prop({ required: true, enum: PROMOTION_SCHEME_TYPES })
  type!: PromotionSchemeType;

  @Prop({ required: true, default: 100 })
  priority!: number;

  @Prop({ required: true, default: true })
  isActive!: boolean;

  @Prop({ required: true, enum: PROMOTION_STACKING_MODES, default: 'best_benefit' })
  stacking!: PromotionStackingMode;

  @Prop({ type: [String], default: [] })
  storeIds!: string[];

  @Prop()
  validFrom?: Date;

  @Prop()
  validTo?: Date;

  @Prop({ type: [PromotionTimeWindow], default: [] })
  timeWindows!: PromotionTimeWindow[];

  @Prop({ type: PromotionConditions, default: {} })
  conditions!: PromotionConditions;

  @Prop({ type: PromotionBenefit, required: true })
  benefit!: PromotionBenefit;

  @Prop()
  deletedAt?: Date;
}

export const PromotionSchemeSchema = SchemaFactory.createForClass(PromotionScheme);
PromotionSchemeSchema.index({ isActive: 1, storeIds: 1 });
PromotionSchemeSchema.index({ updatedAt: 1 });
