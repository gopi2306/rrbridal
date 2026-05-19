import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type CategoryDocument = HydratedDocument<Category>;

@Schema({ timestamps: true, collection: 'categories' })
export class Category {
  @ApiProperty({ example: 'cat-001', description: 'Auto-generated on create (cat-NNN).' })
  @Prop({ required: true, unique: true, trim: true, lowercase: true, index: true })
  code!: string;

  @ApiProperty({ example: 'Clothing' })
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  departmentId?: string;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true, index: true })
  isActive!: boolean;
}

export const CategorySchema = SchemaFactory.createForClass(Category);
