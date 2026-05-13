import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type PackedConfirmationDocument = HydratedDocument<PackedConfirmation>;

@Schema({ timestamps: true, collection: 'packed_confirmations' })
export class PackedConfirmation {
  @ApiProperty({ example: 'pc-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Confirmed' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const PackedConfirmationSchema = SchemaFactory.createForClass(PackedConfirmation);
