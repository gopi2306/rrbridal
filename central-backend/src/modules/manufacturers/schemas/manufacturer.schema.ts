import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ManufacturerDocument = HydratedDocument<Manufacturer>;

@Schema({ timestamps: true, collection: 'manufacturers' })
export class Manufacturer {
  @ApiProperty({ example: 'mfr-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Acme Corp' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  contactPerson?: string;

  @ApiProperty({ required: false })
  @Prop()
  phone?: string;

  @ApiProperty({ required: false })
  @Prop()
  address?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const ManufacturerSchema = SchemaFactory.createForClass(Manufacturer);
