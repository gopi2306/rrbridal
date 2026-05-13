import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type LocationDocument = HydratedDocument<Location>;

@Schema({ timestamps: true, collection: 'locations' })
export class Location {
  @ApiProperty({ example: 'loc-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Warehouse A' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  address?: string;

  @ApiProperty({ required: false, example: 'warehouse' })
  @Prop({ index: true })
  type?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const LocationSchema = SchemaFactory.createForClass(Location);
