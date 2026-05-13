import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type DivisionDocument = HydratedDocument<Division>;

@Schema({ timestamps: true, collection: 'divisions' })
export class Division {
  @ApiProperty({ example: 'div-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Retail' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const DivisionSchema = SchemaFactory.createForClass(Division);
