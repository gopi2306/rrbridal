import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type RoleDefinitionDocument = HydratedDocument<RoleDefinition>;

@Schema({ collection: 'role_definitions', timestamps: true })
export class RoleDefinition {
  @ApiProperty()
  @Prop({ required: true, unique: true })
  code!: string;

  @ApiProperty()
  @Prop({ required: true })
  displayName!: string;

  @ApiProperty({ required: false })
  @Prop()
  description?: string;

  @ApiProperty()
  @Prop({ required: true, default: 0 })
  sortOrder!: number;

  @ApiProperty({ default: true })
  @Prop({ required: true, default: true })
  isActive!: boolean;

  @ApiProperty({ required: false, description: 'Set when role is soft-deleted' })
  @Prop()
  deletedAt?: Date;
}

export const RoleDefinitionSchema = SchemaFactory.createForClass(RoleDefinition);
RoleDefinitionSchema.index({ deletedAt: 1 });
RoleDefinitionSchema.index({ isActive: 1, sortOrder: 1 });
