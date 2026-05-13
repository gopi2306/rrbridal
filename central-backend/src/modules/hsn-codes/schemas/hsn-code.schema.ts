import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type HsnCodeDocument = HydratedDocument<HsnCode>;

@Schema({ timestamps: true, collection: 'hsn_codes' })
export class HsnCode {
  @ApiProperty({ example: 'hsn-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'HSN 6204' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ example: '6204' })
  @Prop({ required: true, index: true })
  hsnCode!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false, example: 18 })
  @Prop()
  gstPercent?: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const HsnCodeSchema = SchemaFactory.createForClass(HsnCode);
