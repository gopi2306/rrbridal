import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type CustomerDocument = HydratedDocument<Customer>;

@Schema({ timestamps: true })
export class Customer {
  @ApiProperty({ required: false })
  @Prop({ index: true, unique: true, sparse: true })
  customerCode?: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  phone?: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  email?: string;

  @ApiProperty({ required: false })
  @Prop()
  gstin?: string;

  @ApiProperty({ required: false })
  @Prop()
  addressLine1?: string;

  @ApiProperty({ required: false })
  @Prop()
  addressLine2?: string;

  @ApiProperty({ required: false })
  @Prop()
  city?: string;

  @ApiProperty({ required: false })
  @Prop()
  state?: string;

  @ApiProperty({ required: false })
  @Prop()
  pincode?: string;

  @ApiProperty({ required: false, default: true })
  @Prop({ default: true, index: true })
  isActive!: boolean;
}

export const CustomerSchema = SchemaFactory.createForClass(Customer);

