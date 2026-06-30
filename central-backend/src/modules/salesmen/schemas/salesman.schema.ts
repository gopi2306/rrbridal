import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type SalesmanDocument = HydratedDocument<Salesman>;

@Schema({ timestamps: true, collection: 'salesmen' })
export class Salesman {
  @ApiProperty()
  @Prop({ required: true, trim: true, index: true })
  storeId!: string;

  @ApiProperty()
  @Prop({ required: true, trim: true })
  salesmanCode!: string;

  @ApiProperty()
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  phone?: string;

  @ApiProperty({ default: true })
  @Prop({ default: true, index: true })
  isActive!: boolean;
}

export const SalesmanSchema = SchemaFactory.createForClass(Salesman);

SalesmanSchema.index({ storeId: 1, salesmanCode: 1 }, { unique: true });
SalesmanSchema.index({ storeId: 1, name: 1 });
