import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type PoQtyPolicyDocument = HydratedDocument<PoQtyPolicy>;

@Schema({ timestamps: true, collection: 'po_qty_policies' })
export class PoQtyPolicy {
  @ApiProperty({ example: 'pqp-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Standard Policy' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false })
  @Prop()
  minQty?: number;

  @ApiProperty({ required: false })
  @Prop()
  maxQty?: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const PoQtyPolicySchema = SchemaFactory.createForClass(PoQtyPolicy);
