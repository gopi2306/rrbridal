import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type OfferGroupDocument = HydratedDocument<OfferGroup>;

@Schema({ timestamps: true, collection: 'offer_groups' })
export class OfferGroup {
  @ApiProperty({ example: 'og-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Festival Offer' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty({ required: false })
  @Prop()
  discountPercent?: number;

  @ApiProperty({ required: false })
  @Prop()
  validFrom?: string;

  @ApiProperty({ required: false })
  @Prop()
  validTo?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const OfferGroupSchema = SchemaFactory.createForClass(OfferGroup);
