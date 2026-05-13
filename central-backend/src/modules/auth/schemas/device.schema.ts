import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type DeviceDocument = HydratedDocument<Device>;

@Schema({ timestamps: true })
export class Device {
  @ApiProperty()
  @Prop({ required: true, index: true })
  storeId!: string;

  @ApiProperty()
  @Prop({ required: true, unique: true, index: true })
  deviceId!: string;

  // NOTE: for demo scaffolding this is a plain secret.
  // In production, store a hash (bcrypt/argon2) and rotate periodically.
  @Prop({ required: true })
  deviceSecret!: string;

  @Prop({ default: true })
  isActive!: boolean;
}

export const DeviceSchema = SchemaFactory.createForClass(Device);

