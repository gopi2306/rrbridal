import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type SkuOrderGroupDocument = HydratedDocument<SkuOrderGroup>;

@Schema({ timestamps: true, collection: 'sku_order_groups' })
export class SkuOrderGroup {
  @ApiProperty({ example: 'sog-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Group A' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false })
  @Prop()
  sortOrder?: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const SkuOrderGroupSchema = SchemaFactory.createForClass(SkuOrderGroup);
