import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ProductStatusDocument = HydratedDocument<ProductStatus>;

@Schema({ timestamps: true, collection: 'product_statuses' })
export class ProductStatus {
  @ApiProperty({ example: 'ps-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Active' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const ProductStatusSchema = SchemaFactory.createForClass(ProductStatus);
