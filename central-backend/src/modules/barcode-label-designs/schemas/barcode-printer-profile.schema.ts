import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type BarcodePrinterProfileDocument = HydratedDocument<BarcodePrinterProfile>;

@Schema({ timestamps: true, collection: 'barcode_printer_profiles' })
export class BarcodePrinterProfile {
  @ApiProperty({ description: 'Stable profile id, e.g. tsc-ttp-244-pro' })
  @Prop({ required: true, unique: true, trim: true })
  profileId!: string;

  @ApiProperty()
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop({ trim: true })
  manufacturer?: string;

  @ApiProperty()
  @Prop({ required: true, min: 1 })
  dpi!: number;

  @ApiProperty()
  @Prop({ required: true, min: 1 })
  labelWidthMm!: number;

  @ApiProperty()
  @Prop({ required: true, min: 1 })
  labelHeightMm!: number;

  @ApiProperty()
  @Prop({ required: true, min: 1, default: 1 })
  labelsPerRow!: number;
}

export const BarcodePrinterProfileSchema = SchemaFactory.createForClass(BarcodePrinterProfile);
