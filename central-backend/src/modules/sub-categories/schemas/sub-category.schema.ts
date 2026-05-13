import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type SubCategoryDocument = HydratedDocument<SubCategory>;

@Schema({ timestamps: true, collection: 'sub_categories' })
export class SubCategory {
  @ApiProperty({ example: 'subcat-001' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Bridal Lehenga' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  categoryId!: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const SubCategorySchema = SchemaFactory.createForClass(SubCategory);
