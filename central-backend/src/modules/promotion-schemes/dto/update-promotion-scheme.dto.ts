import { ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsDateString,
  IsIn,
  IsInt,
  IsObject,
  IsOptional,
  IsString,
  Min,
  ValidateNested,
} from 'class-validator';
import {
  PROMOTION_SCHEME_KINDS,
  PROMOTION_SCHEME_TYPES,
  PROMOTION_STACKING_MODES,
} from '../schemas/promotion-scheme.schema';
import { PromotionConditionsDto, PromotionTimeWindowDto, PromotionBenefitDto } from './promotion-scheme-shared.dto';

export class UpdatePromotionSchemeDto {
  @ApiPropertyOptional()
  @IsString()
  @IsOptional()
  name?: string;

  @ApiPropertyOptional()
  @IsString()
  @IsOptional()
  description?: string;

  @ApiPropertyOptional({ enum: PROMOTION_SCHEME_KINDS })
  @IsIn([...PROMOTION_SCHEME_KINDS])
  @IsOptional()
  kind?: string;

  @ApiPropertyOptional({ enum: PROMOTION_SCHEME_TYPES })
  @IsIn([...PROMOTION_SCHEME_TYPES])
  @IsOptional()
  type?: string;

  @ApiPropertyOptional()
  @IsInt()
  @Min(0)
  @IsOptional()
  priority?: number;

  @ApiPropertyOptional()
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;

  @ApiPropertyOptional({ enum: PROMOTION_STACKING_MODES })
  @IsIn([...PROMOTION_STACKING_MODES])
  @IsOptional()
  stacking?: string;

  @ApiPropertyOptional({ type: [String] })
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

  @ApiPropertyOptional({ type: PromotionBenefitDto })
  @ValidateNested()
  @Type(() => PromotionBenefitDto)
  @IsOptional()
  benefit?: PromotionBenefitDto;
}
