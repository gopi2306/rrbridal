import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type WeightSizeDocument = HydratedDocument<WeightSize>;

@Schema({ timestamps: true, collection: 'weight_sizes' })
export class WeightSize {
  @ApiProperty({ example: 'ws-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Large' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false, example: 'kg' })
  @Prop()
  unit?: string;

  @ApiProperty({ required: false })
  @Prop()
  value?: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const WeightSizeSchema = SchemaFactory.createForClass(WeightSize);
