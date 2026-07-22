import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { Types } from 'mongoose';

@Schema({ _id: false })
export class ProductMediaItem {
  @ApiProperty({ description: 'Public URL of the product image/file' })
  @Prop({ required: true })
  url!: string;

  @ApiProperty({ required: false, description: 'Optional description for this image' })
  @Prop()
  description?: string;

  @ApiProperty({
    required: false,
    type: [String],
    description: 'Optional colour ids shown in this image',
  })
  @Prop({ type: [{ type: Types.ObjectId, ref: 'Colour' }], default: undefined })
  colourIds?: Types.ObjectId[];
}

export const ProductMediaItemSchema = SchemaFactory.createForClass(ProductMediaItem);
