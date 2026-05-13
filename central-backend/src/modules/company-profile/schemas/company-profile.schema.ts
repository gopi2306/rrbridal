import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

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
}

export const CompanyProfileSchema = SchemaFactory.createForClass(CompanyProfile);
