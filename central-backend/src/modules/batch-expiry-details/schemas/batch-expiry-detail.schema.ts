import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type BatchExpiryDetailDocument = HydratedDocument<BatchExpiryDetail>;

@Schema({ timestamps: true, collection: 'batch_expiry_details' })
export class BatchExpiryDetail {
  @ApiProperty({ example: 'bed-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Batch 2024' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const BatchExpiryDetailSchema = SchemaFactory.createForClass(BatchExpiryDetail);
