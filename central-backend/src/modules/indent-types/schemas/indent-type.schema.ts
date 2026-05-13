import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type IndentTypeDocument = HydratedDocument<IndentType>;

@Schema({ timestamps: true, collection: 'indent_types' })
export class IndentType {
  @ApiProperty({ example: 'it-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Standard Indent' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const IndentTypeSchema = SchemaFactory.createForClass(IndentType);
