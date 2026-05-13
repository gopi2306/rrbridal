import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ColourDocument = HydratedDocument<Colour>;

@Schema({ timestamps: true, collection: 'colours' })
export class Colour {
  @ApiProperty({ example: 'clr-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Red' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false, example: '#FF0000' })
  @Prop()
  hexCode?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const ColourSchema = SchemaFactory.createForClass(Colour);
