import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type RoleDefinitionDocument = HydratedDocument<RoleDefinition>;

@Schema({ collection: 'role_definitions' })
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
}

export const RoleDefinitionSchema = SchemaFactory.createForClass(RoleDefinition);
