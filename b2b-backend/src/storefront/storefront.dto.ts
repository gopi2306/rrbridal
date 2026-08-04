import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsEmail,
  IsEnum,
  IsInt,
  IsMongoId,
  IsNotEmpty,
  IsOptional,
  IsString,
  Max,
  Min,
  ValidateNested,
} from 'class-validator';

export class StorefrontCatalogQueryDto {
  @IsOptional()
  @IsString()
  search?: string;

  @IsOptional()
  @IsString()
  category?: string;

  @IsOptional()
  @Type(() => Number)
  @Min(0)
  minPrice?: number;

  @IsOptional()
  @Type(() => Number)
  @Min(0)
  maxPrice?: number;

  @IsOptional()
  @IsEnum(['featured', 'price-asc', 'price-desc', 'name'])
  sort: 'featured' | 'price-asc' | 'price-desc' | 'name' = 'featured';

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  page = 1;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  limit = 24;
}

export class StorefrontRetailerDto {
  @IsString()
  @IsNotEmpty()
  businessName!: string;

  @IsString()
  @IsNotEmpty()
  contactName!: string;

  @IsEmail()
  email!: string;

  @IsString()
  @IsNotEmpty()
  phone!: string;

  @IsOptional()
  @IsString()
  address?: string;

  @IsOptional()
  @IsString()
  city?: string;

  @IsOptional()
  @IsString()
  state?: string;

  @IsOptional()
  @IsString()
  pincode?: string;
}

export class StorefrontEnquiryLineDto {
  @IsString()
  @IsNotEmpty()
  databaseKey!: string;

  @IsMongoId()
  productId!: string;

  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100000)
  quantity!: number;
}

export class StorefrontEnquiryDto {
  @IsOptional()
  @IsString()
  requestId?: string;

  @ValidateNested()
  @Type(() => StorefrontRetailerDto)
  retailer!: StorefrontRetailerDto;

  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => StorefrontEnquiryLineDto)
  lines!: StorefrontEnquiryLineDto[];

  @IsOptional()
  @IsString()
  note?: string;
}
