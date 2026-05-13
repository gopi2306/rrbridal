import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type UomSubDocument = HydratedDocument<UomSub>;

@Schema({ timestamps: true, collection: 'uom_subs' })
export class UomSub {
  @ApiProperty({ example: 'usub-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Sub Unit' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  baseUom?: string;

  @ApiProperty({ required: false })
  @Prop()
  conversionFactor?: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const UomSubSchema = SchemaFactory.createForClass(UomSub);
