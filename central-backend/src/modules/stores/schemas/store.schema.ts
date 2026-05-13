import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type StoreDocument = HydratedDocument<Store>;
export type StoreStatus = 'active' | 'inactive';

@Schema({ timestamps: true, collection: 'stores' })
export class Store {
  @ApiProperty({ example: 'store-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'RR Bridal - Main Branch' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  address?: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  phone?: string;

  @ApiProperty({ enum: ['active', 'inactive'], default: 'active' })
  @Prop({ required: true, default: 'active', index: true })
  status!: StoreStatus;
}

export const StoreSchema = SchemaFactory.createForClass(Store);
