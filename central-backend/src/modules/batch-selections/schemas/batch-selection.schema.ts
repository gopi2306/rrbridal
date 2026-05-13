import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type BatchSelectionDocument = HydratedDocument<BatchSelection>;

@Schema({ timestamps: true, collection: 'batch_selections' })
export class BatchSelection {
  @ApiProperty({ example: 'bs-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Default Batch' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const BatchSelectionSchema = SchemaFactory.createForClass(BatchSelection);
