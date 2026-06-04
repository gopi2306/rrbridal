import { ApiProperty } from '@nestjs/swagger';
import {
  IsEmail,
  IsIn,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  Max,
  Min,
  MinLength,
  ValidateIf,
} from 'class-validator';

const roles = ['admin', 'warehouse', 'store', 'procurement'] as const;
const locations = ['all', 'warehouse', 'store'] as const;
const statuses = ['active', 'invited', 'disabled'] as const;

export class CreateUserDto {
  @ApiProperty()
  @IsEmail()
  @IsNotEmpty()
  email!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @MinLength(8)
  password!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  name!: string;

  @ApiProperty({ enum: roles })
  @IsIn(roles)
  role!: (typeof roles)[number];

  @ApiProperty({ enum: locations })
  @IsIn(locations)
  locationKind!: (typeof locations)[number];

  @ApiProperty({ required: false })
  @ValidateIf((o: CreateUserDto) => o.role === 'store' || o.locationKind === 'store')
  @IsString()
  @IsNotEmpty()
  storeId?: string;

  @ApiProperty({
    required: false,
    description: 'Location.code of an active warehouse; required when role is warehouse and locationKind is warehouse',
  })
  @ValidateIf((o: CreateUserDto) => o.role === 'warehouse' && o.locationKind === 'warehouse')
  @IsString()
  @IsNotEmpty()
  warehouseLocationCode?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  @ApiProperty({
    required: false,
    minimum: 0,
    maximum: 100,
    description: 'Max combined manual discount % for store billing (item % + cash ₹)',
  })
  @IsOptional()
  @IsNumber()
  @Min(0)
  @Max(100)
  maxDiscountPercent?: number;
}
