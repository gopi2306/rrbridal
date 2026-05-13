import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsEmail, IsNotEmpty, IsOptional, IsString } from 'class-validator';

export class CreateSupplierDto {
  // ── Contact Master (Admin) ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  title?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  businessRelatedType?: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  name!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  contactAdmin?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  contactBusiness?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  firstName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  nickName?: string;

  // ── GST Information ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstNumber?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstStateCode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  gstRegistrationType?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  panNumber?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  aadharNo?: string;

  // ── User Login Details ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  systemAccess?: string;

  // ── Contact Numbers ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  contactPerson?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  contactDescription?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  offPhoneNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  offExtensionNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  resPhoneNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  resExtensionNo?: string;

  @ApiProperty({ required: false })
  @IsEmail()
  @IsOptional()
  emailId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  faxNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mobileNo?: string;

  // ── Contact Address ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  buildingAddress?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  streetAddress?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  landmark?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  country?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  state?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  city?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  pin?: string;

  // ── Contact Types ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  attributeValuesOutlet?: string;

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isSupplier?: boolean;

  // ── Attachment ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  fileDescription?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  uploadFile?: string;

  // ── HQ Role Information ──

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  chooseJobOutlet?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  chooseHqRole?: string;

  // ── Status ──

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;
}
