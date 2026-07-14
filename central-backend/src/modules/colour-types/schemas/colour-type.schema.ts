import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ColourTypeDocument = HydratedDocument<ColourType>;

@Schema({ timestamps: true, collection: 'colour_types' })
export class ColourType {
  @ApiProperty({ example: 'ct-1' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: '1 Color' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const ColourTypeSchema = SchemaFactory.createForClass(ColourType);
