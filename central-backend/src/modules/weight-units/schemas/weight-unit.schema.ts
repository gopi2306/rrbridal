import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type WeightUnitDocument = HydratedDocument<WeightUnit>;

@Schema({ timestamps: true, collection: 'weight_units' })
export class WeightUnit {
  @ApiProperty({ example: 'wu-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Kilogram' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false, example: 'gm' })
  @Prop()
  baseUnit?: string;

  @ApiProperty({ required: false, example: 1000 })
  @Prop()
  conversionFactor?: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const WeightUnitSchema = SchemaFactory.createForClass(WeightUnit);
