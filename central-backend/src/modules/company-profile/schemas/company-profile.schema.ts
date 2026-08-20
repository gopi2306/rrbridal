import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';
import { ReceiptQrSlot, ReceiptQrSlotSchema } from './receipt-qr-slot.schema';

export type CompanyProfileDocument = HydratedDocument<CompanyProfile>;

export const COMPANY_PROFILE_KEY = 'default';

@Schema({ timestamps: true, collection: 'company_profile' })
export class CompanyProfile {
  @ApiProperty({ description: 'Singleton document key' })
  @Prop({ required: true, unique: true, default: COMPANY_PROFILE_KEY })
  settingsKey!: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  legalName?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  tradeName?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  gstin?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  address?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  city?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  state?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  pinCode?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  phone?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  email?: string;

  @ApiProperty({ required: false, description: 'URL of the company logo image' })
  @Prop({ trim: true })
  companyLogo?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  fssaiNo?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  website?: string;

  @ApiProperty({ required: false, description: 'Bank account holder for Tax Invoice' })
  @Prop({ trim: true })
  bankAccountHolderName?: string;

  @ApiProperty({ required: false, description: 'Bank account number for Tax Invoice' })
  @Prop({ trim: true })
  bankAccountNumber?: string;

  @ApiProperty({ required: false, description: 'Bank IFSC for Tax Invoice' })
  @Prop({ trim: true })
  bankIfsc?: string;

  @ApiProperty({ required: false, description: 'Bank branch name for Tax Invoice' })
  @Prop({ trim: true })
  bankBranchName?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  termsAndConditions?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  thankYouLine?: string;

  @ApiProperty({ required: false, type: [String] })
  @Prop({ type: [String] })
  policyLines?: string[];

  @ApiProperty({ required: false, type: [ReceiptQrSlot] })
  @Prop({ type: [ReceiptQrSlotSchema] })
  receiptQrSlots?: ReceiptQrSlot[];

  @ApiProperty({ required: false, default: true })
  @Prop({ default: true })
  receiptBarcodeEnabled?: boolean;

  @ApiProperty({
    required: false,
    description: 'Additional company metadata as arbitrary key-value pairs',
    example: { website: 'https://example.com', tagline: 'Since 1990' },
  })
  @Prop({ type: Object })
  extraFields?: Record<string, unknown>;
}

export const CompanyProfileSchema = SchemaFactory.createForClass(CompanyProfile);
