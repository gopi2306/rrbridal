import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type RoleAccessStatus = 'active' | 'inactive';

export type RoleAccessDocument = HydratedDocument<RoleAccess>;

@Schema({ timestamps: true, collection: 'role_access' })
export class RoleAccess {
  @ApiProperty({ example: 'admin', description: 'Role code from role_definitions' })
  @Prop({ required: true, trim: true, lowercase: true, index: true })
  role!: string;

  @ApiProperty({ example: 'core' })
  @Prop({ required: true, trim: true, lowercase: true, index: true })
  area!: string;

  @ApiProperty({ example: 'Dashboard' })
  @Prop({ required: true, trim: true })
  screen!: string;

  @ApiProperty({ default: false })
  @Prop({ required: true, default: false })
  allow!: boolean;

  @ApiProperty({ enum: ['active', 'inactive'], default: 'active' })
  @Prop({ required: true, enum: ['active', 'inactive'], default: 'active', index: true })
  status!: RoleAccessStatus;
}

export const RoleAccessSchema = SchemaFactory.createForClass(RoleAccess);
RoleAccessSchema.index({ role: 1, area: 1, screen: 1 }, { unique: true });
