import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type BranchDocument = HydratedDocument<Branch>;

@Schema({ timestamps: true, collection: 'branches' })
export class Branch {
  @ApiProperty({ example: 'br-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Chennai Main' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  address?: string;

  @ApiProperty({ required: false })
  @Prop()
  phone?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const BranchSchema = SchemaFactory.createForClass(Branch);
