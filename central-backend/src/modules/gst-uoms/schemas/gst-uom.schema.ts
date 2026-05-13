import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type GstUomDocument = HydratedDocument<GstUom>;

@Schema({ timestamps: true, collection: 'gst_uoms' })
export class GstUom {
  @ApiProperty({ example: 'guom-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Pieces' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const GstUomSchema = SchemaFactory.createForClass(GstUom);
