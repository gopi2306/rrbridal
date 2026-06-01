import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsDateString,
  IsIn,
  IsInt,
  IsNumber,
  IsOptional,
  IsString,
  Max,
  Min,
  ValidateNested,
} from 'class-validator';
import {
  PROMOTION_ITEM_BENEFIT_MODES,
  PROMOTION_SCHEME_KINDS,
  PROMOTION_SCHEME_TYPES,
  PROMOTION_STACKING_MODES,
  PROMOTION_FREE_ON,
} from '../schemas/promotion-scheme.schema';

export class PromotionTimeWindowDto {
  @ApiProperty({ minimum: 0, maximum: 6, description: '0=Sunday … 6=Saturday' })
  @IsInt()
  @Min(0)
  @Max(6)
  dayOfWeek!: number;

  @ApiProperty({ minimum: 0, maximum: 23 })
  @IsInt()
  @Min(0)
  @Max(23)
  fromHour!: number;

  @ApiProperty({ minimum: 0, maximum: 23 })
  @IsInt()
  @Min(0)
  @Max(23)
  toHour!: number;
}

export class PromotionComboRequirementDto {
  @ApiProperty()
  @IsString()
  sku!: string;

  @ApiProperty({ minimum: 1 })
  @IsNumber()
  @Min(1)
  requiredQty!: number;
}

export class PromotionConditionsDto {
  @ApiPropertyOptional({ type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  skus?: string[];

  @ApiPropertyOptional({ type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  categoryIds?: string[];

  @ApiPropertyOptional({ type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  brandIds?: string[];

  @ApiPropertyOptional({ type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  offerGroupIds?: string[];

  @ApiPropertyOptional({ minimum: 0 })
  @IsNumber()
  @Min(0)
  @IsOptional()
  minLineQty?: number;

  @ApiPropertyOptional({ minimum: 0 })
  @IsNumber()
  @Min(0)
  @IsOptional()
  minBillAmount?: number;

  @ApiPropertyOptional({ type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  customerTypes?: string[];

  @ApiPropertyOptional({ type: [String] })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  customerCodes?: string[];

  @ApiPropertyOptional({ type: [PromotionComboRequirementDto] })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => PromotionComboRequirementDto)
  @IsOptional()
  requiredSkus?: PromotionComboRequirementDto[];
}

export class PromotionSlabDto {
  @ApiProperty({ minimum: 0 })
  @IsNumber()
  @Min(0)
  fromAmount!: number;

  @ApiPropertyOptional({ minimum: 0 })
  @IsNumber()
  @Min(0)
  @IsOptional()
  toAmount?: number;

  @ApiProperty({ minimum: 0, maximum: 100 })
  @IsNumber()
  @Min(0)
  @Max(100)
  discountPercent!: number;
}

export class PromotionBenefitDto {
  @ApiPropertyOptional({ enum: PROMOTION_ITEM_BENEFIT_MODES, description: 'Required when type=item' })
  @IsIn([...PROMOTION_ITEM_BENEFIT_MODES])
  @IsOptional()
  mode?: string;

  @ApiPropertyOptional({ minimum: 1, description: 'Buy X Get Y — buy quantity' })
  @IsNumber()
  @Min(1)
  @IsOptional()
  buyQty?: number;

  @ApiPropertyOptional({ minimum: 1, description: 'Buy X Get Y — free quantity' })
  @IsNumber()
  @Min(1)
  @IsOptional()
  getQty?: number;

  @ApiPropertyOptional({ enum: PROMOTION_FREE_ON, default: 'cheapest' })
  @IsIn([...PROMOTION_FREE_ON])
  @IsOptional()
  freeOn?: string;

  @ApiPropertyOptional({ minimum: 0, maximum: 100 })
  @IsNumber()
  @Min(0)
  @Max(100)
  @IsOptional()
  discountPercent?: number;

  @ApiPropertyOptional({ minimum: 0 })
  @IsNumber()
  @Min(0)
  @IsOptional()
  flatAmount?: number;

  @ApiPropertyOptional({ minimum: 0, description: 'Bill threshold (₹)' })
  @IsNumber()
  @Min(0)
  @IsOptional()
  minBillAmount?: number;

  @ApiPropertyOptional({ type: [PromotionSlabDto], description: 'Required when type=slab' })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => PromotionSlabDto)
  @IsOptional()
  slabs?: PromotionSlabDto[];

  @ApiPropertyOptional({ type: [String], description: 'Required when type=combo' })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  comboSkus?: string[];

  @ApiPropertyOptional({ minimum: 0, description: 'Combo bundle price (₹)' })
  @IsNumber()
  @Min(0)
  @IsOptional()
  fixedPrice?: number;
}

export class PromotionSchemeBaseDto {
  @ApiProperty()
  @IsString()
  code!: string;

  @ApiProperty()
  @IsString()
  name!: string;

  @ApiPropertyOptional()
  @IsString()
  @IsOptional()
  description?: string;

  @ApiPropertyOptional({ enum: PROMOTION_SCHEME_KINDS })
  @IsIn([...PROMOTION_SCHEME_KINDS])
  @IsOptional()
  kind?: string;

  @ApiProperty({ enum: PROMOTION_SCHEME_TYPES })
  @IsIn([...PROMOTION_SCHEME_TYPES])
  type!: string;

  @ApiPropertyOptional({ default: 100 })
  @IsInt()
  @Min(0)
  @IsOptional()
  priority?: number;

  @ApiPropertyOptional({ default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;

  @ApiPropertyOptional({ enum: PROMOTION_STACKING_MODES })
  @IsIn([...PROMOTION_STACKING_MODES])
  @IsOptional()
  stacking?: string;

  @ApiPropertyOptional({ type: [String], description: 'Empty = all stores' })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  storeIds?: string[];

  @ApiPropertyOptional()
  @IsDateString()
  @IsOptional()
  validFrom?: string;

  @ApiPropertyOptional()
  @IsDateString()
  @IsOptional()
  validTo?: string;

  @ApiPropertyOptional({ type: [PromotionTimeWindowDto] })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => PromotionTimeWindowDto)
  @IsOptional()
  timeWindows?: PromotionTimeWindowDto[];

  @ApiPropertyOptional({ type: PromotionConditionsDto })
  @ValidateNested()
  @Type(() => PromotionConditionsDto)
  @IsOptional()
  conditions?: PromotionConditionsDto;

  @ApiProperty({ type: PromotionBenefitDto, description: 'Discount rules — fields used depend on scheme type' })
  @ValidateNested()
  @Type(() => PromotionBenefitDto)
  benefit!: PromotionBenefitDto;
}
