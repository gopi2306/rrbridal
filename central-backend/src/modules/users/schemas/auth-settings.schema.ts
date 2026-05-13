import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type AuthSettingsDocument = HydratedDocument<AuthSettings>;

@Schema({ timestamps: true, collection: 'auth_settings' })
export class AuthSettings {
  @ApiProperty({ description: 'Singleton key' })
  @Prop({ required: true, unique: true, default: 'default' })
  settingsKey!: string;

  @ApiProperty({ description: 'Max users per role (active + invited count toward quota)' })
  @Prop({ type: Object, required: true })
  roleQuotas!: Record<string, number>;
}

export const AuthSettingsSchema = SchemaFactory.createForClass(AuthSettings);
