import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type SellByTypeDocument = HydratedDocument<SellByType>;

@Schema({ timestamps: true, collection: 'sell_by_types' })
export class SellByType {
  @ApiProperty({ example: 'sbt-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Retail' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const SellByTypeSchema = SchemaFactory.createForClass(SellByType);
