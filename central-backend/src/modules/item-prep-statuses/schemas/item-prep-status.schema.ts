import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ItemPrepStatusDocument = HydratedDocument<ItemPrepStatus>;

@Schema({ timestamps: true, collection: 'item_prep_statuses' })
export class ItemPrepStatus {
  @ApiProperty({ example: 'ips-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Ready to Ship' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const ItemPrepStatusSchema = SchemaFactory.createForClass(ItemPrepStatus);
