import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsBoolean,
  IsEmail,
  IsEnum,
  IsInt,
  IsMongoId,
  IsNotEmpty,
  IsNumber,
  IsObject,
  IsOptional,
  IsString,
  Max,
  Min,
  ValidateNested,
} from 'class-validator';

export class ScopeQueryDto {
  @IsOptional()
  @IsString()
  databaseKey?: string;

  @IsOptional()
  @IsString()
  search?: string;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  page = 1;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  limit = 50;
}

export class InventoryQueryDto extends ScopeQueryDto {
  @IsOptional()
  @IsEnum(['warehouse', 'store', 'in_transit'])
  locationKind?: 'warehouse' | 'store' | 'in_transit';

  @IsOptional()
  @IsString()
  storeId?: string;
}

export class DateRangeQueryDto {
  @IsOptional()
  @IsString()
  databaseKey?: string;

  @IsOptional()
  @IsString()
  from?: string;

  @IsOptional()
  @IsString()
  to?: string;
}

export class DatabaseWriteDto {
  @IsString()
  @IsNotEmpty()
  databaseKey!: string;
}

export class InventoryAdjustmentDto extends DatabaseWriteDto {
  @IsString()
  @IsNotEmpty()
  sku!: string;

  @IsNumber()
  qtyDelta!: number;

  @IsEnum(['warehouse', 'store', 'in_transit'])
  locationKind!: 'warehouse' | 'store' | 'in_transit';

  @IsOptional()
  @IsString()
  storeId?: string;

  @IsOptional()
  @IsString()
  locationCode?: string;

  @IsString()
  @IsNotEmpty()
  sourceId!: string;

  @IsOptional()
  @IsString()
  note?: string;
}

export class CustomerWriteDto extends DatabaseWriteDto {
  @IsOptional()
  @IsString()
  customerCode?: string;

  @IsString()
  @IsNotEmpty()
  name!: string;

  @IsOptional()
  @IsString()
  phone?: string;

  @IsOptional()
  @IsEmail()
  email?: string;

  @IsOptional()
  @IsString()
  gstin?: string;

  @IsOptional()
  @IsString()
  addressLine1?: string;

  @IsOptional()
  @IsString()
  addressLine2?: string;

  @IsOptional()
  @IsString()
  city?: string;

  @IsOptional()
  @IsString()
  state?: string;

  @IsOptional()
  @IsString()
  pincode?: string;

  @IsOptional()
  @IsBoolean()
  isActive?: boolean;

  @IsOptional()
  @IsBoolean()
  isCreditCustomer?: boolean;
}

export class StoreWriteDto extends DatabaseWriteDto {
  @IsString()
  @IsNotEmpty()
  code!: string;

  @IsString()
  @IsNotEmpty()
  name!: string;

  @IsOptional()
  @IsString()
  address?: string;

  @IsOptional()
  @IsString()
  phone?: string;

  @IsOptional()
  @IsEnum(['active', 'inactive'])
  status?: 'active' | 'inactive';

  @IsOptional()
  @IsBoolean()
  preferCentralOnline?: boolean;
}

export class ProductPatchDto extends DatabaseWriteDto {
  @IsOptional()
  @IsString()
  itemName?: string;

  @IsOptional()
  @IsString()
  shortName?: string;

  @IsOptional()
  @IsString()
  alias?: string;

  @IsOptional()
  @IsString()
  upcEanCode?: string;

  @IsOptional()
  @IsNumber()
  costPrice?: number;

  @IsOptional()
  @IsNumber()
  mrp?: number;

  @IsOptional()
  @IsNumber()
  sellingPrice?: number;

  @IsOptional()
  @IsNumber()
  storePrice?: number;

  @IsOptional()
  @IsBoolean()
  isActive?: boolean;

  @IsOptional()
  @IsBoolean()
  isAddedInB2B?: boolean;
}

export class PurchaseOrderSupplierDto {
  @IsString()
  @IsNotEmpty()
  supplierId!: string;

  @IsOptional()
  @IsString()
  code?: string;

  @IsOptional()
  @IsString()
  name?: string;

  @IsOptional()
  @IsString()
  mobile?: string;
}

export class PurchaseOrderLineDto {
  @IsOptional()
  @IsMongoId()
  productId?: string;

  @IsString()
  @IsNotEmpty()
  sku!: string;

  @IsOptional()
  @IsString()
  description?: string;

  @IsOptional()
  @IsNumber()
  recdQty?: number;

  @IsOptional()
  @IsNumber()
  cost?: number;

  @IsOptional()
  @IsNumber()
  selling?: number;

  @IsOptional()
  @IsNumber()
  mrp?: number;

  @IsOptional()
  @IsNumber()
  netAmount?: number;
}

export class PurchaseOrderWriteDto extends DatabaseWriteDto {
  @IsString()
  @IsNotEmpty()
  poNo!: string;

  @ValidateNested()
  @Type(() => PurchaseOrderSupplierDto)
  supplier!: PurchaseOrderSupplierDto;

  @IsOptional()
  @IsString()
  poDate?: string;

  @IsOptional()
  @IsString()
  deliveryDate?: string;

  @IsOptional()
  @IsEnum(['open', 'awaiting_approval', 'approved', 'partially_received', 'received', 'closed'])
  status?: string;

  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => PurchaseOrderLineDto)
  lines!: PurchaseOrderLineDto[];

  @IsOptional()
  @IsNumber()
  netAmount?: number;
}

export class PurchaseOrderStatusDto extends DatabaseWriteDto {
  @IsEnum(['open', 'awaiting_approval', 'approved', 'partially_received', 'received', 'closed'])
  status!: string;
}

export class FlexiblePatchDto extends DatabaseWriteDto {
  @IsObject()
  changes!: Record<string, unknown>;
}
