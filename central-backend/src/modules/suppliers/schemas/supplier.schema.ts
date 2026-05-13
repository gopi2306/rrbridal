import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type SupplierDocument = HydratedDocument<Supplier>;

@Schema({ timestamps: true })
export class Supplier {
  // ── Contact Master (Admin) ──

  @ApiProperty({ required: false })
  @Prop()
  title?: string;

  @ApiProperty({ required: false })
  @Prop()
  businessRelatedType?: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  contactAdmin?: string;

  @ApiProperty({ required: false })
  @Prop()
  contactBusiness?: string;

  @ApiProperty({ required: false })
  @Prop()
  firstName?: string;

  @ApiProperty({ required: false })
  @Prop()
  nickName?: string;

  // ── GST Information ──

  @ApiProperty({ required: false })
  @Prop({ index: true })
  gstNumber?: string;

  @ApiProperty({ required: false })
  @Prop()
  gstStateCode?: string;

  @ApiProperty({ required: false })
  @Prop()
  gstRegistrationType?: string;

  @ApiProperty({ required: false })
  @Prop()
  panNumber?: string;

  @ApiProperty({ required: false })
  @Prop()
  aadharNo?: string;

  // ── User Login Details ──

  @ApiProperty({ required: false })
  @Prop()
  systemAccess?: string;

  // ── Contact Numbers ──

  @ApiProperty({ required: false })
  @Prop()
  contactPerson?: string;

  @ApiProperty({ required: false })
  @Prop()
  contactDescription?: string;

  @ApiProperty({ required: false })
  @Prop()
  offPhoneNo?: string;

  @ApiProperty({ required: false })
  @Prop()
  offExtensionNo?: string;

  @ApiProperty({ required: false })
  @Prop()
  resPhoneNo?: string;

  @ApiProperty({ required: false })
  @Prop()
  resExtensionNo?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  emailId?: string;

  @ApiProperty({ required: false })
  @Prop()
  faxNo?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  mobileNo?: string;

  // ── Contact Address ──

  @ApiProperty({ required: false })
  @Prop()
  buildingAddress?: string;

  @ApiProperty({ required: false })
  @Prop()
  streetAddress?: string;

  @ApiProperty({ required: false })
  @Prop()
  landmark?: string;

  @ApiProperty({ required: false })
  @Prop()
  country?: string;

  @ApiProperty({ required: false })
  @Prop()
  state?: string;

  @ApiProperty({ required: false })
  @Prop()
  city?: string;

  @ApiProperty({ required: false })
  @Prop()
  pin?: string;

  // ── Contact Types ──

  @ApiProperty({ required: false })
  @Prop()
  attributeValuesOutlet?: string;

  @ApiProperty({ required: false, default: true })
  @Prop({ default: true })
  isSupplier!: boolean;

  // ── Attachment ──

  @ApiProperty({ required: false })
  @Prop()
  fileDescription?: string;

  @ApiProperty({ required: false })
  @Prop()
  uploadFile?: string;

  // ── HQ Role Information ──

  @ApiProperty({ required: false })
  @Prop()
  chooseJobOutlet?: string;

  @ApiProperty({ required: false })
  @Prop()
  chooseHqRole?: string;

  // ── Status ──

  @ApiProperty({ required: false, default: true })
  @Prop({ default: true, index: true })
  isActive!: boolean;
}

export const SupplierSchema = SchemaFactory.createForClass(Supplier);
