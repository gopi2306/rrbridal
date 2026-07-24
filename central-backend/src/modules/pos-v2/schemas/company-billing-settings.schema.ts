import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type CompanyBillingSettingsDocument = HydratedDocument<CompanyBillingSettings>;

@Schema({ timestamps: true, collection: 'pos_v2_company_billing_settings' })
export class CompanyBillingSettings {
  @ApiProperty({ example: 'store-01' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  storeId!: string;

  @ApiProperty({ enum: ['retail', 'wholesale'], default: 'retail' })
  @Prop({ required: true, default: 'retail', enum: ['retail', 'wholesale'] })
  mode!: 'retail' | 'wholesale';

  @ApiProperty({ default: false })
  @Prop({ required: true, default: false })
  allowPerBillSwitch!: boolean;
}

export const CompanyBillingSettingsSchema = SchemaFactory.createForClass(CompanyBillingSettings);
