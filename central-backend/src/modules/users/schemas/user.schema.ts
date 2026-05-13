import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type UserDocument = HydratedDocument<User>;

export type UserRole = 'admin' | 'warehouse' | 'store' | 'procurement';
export type UserLocationKind = 'all' | 'warehouse' | 'store';
export type UserStatus = 'active' | 'invited' | 'disabled';

@Schema({ timestamps: true, collection: 'users' })
export class User {
  @ApiProperty()
  @Prop({ required: true, unique: true, lowercase: true, trim: true, index: true })
  email!: string;

  @Prop({ required: true, select: false })
  passwordHash!: string;

  @ApiProperty()
  @Prop({ required: true, trim: true })
  name!: string;

  @ApiProperty({ enum: ['admin', 'warehouse', 'store', 'procurement'] })
  @Prop({ required: true, type: String, index: true })
  role!: UserRole;

  @ApiProperty({ enum: ['all', 'warehouse', 'store'] })
  @Prop({ required: true, type: String })
  locationKind!: UserLocationKind;

  @ApiProperty({ required: false })
  @Prop({ index: true })
  storeId?: string;

  @ApiProperty({ required: false, description: 'Location.code when role is warehouse and locationKind is warehouse' })
  @Prop({ trim: true, lowercase: true, index: true })
  warehouseLocationCode?: string;

  @ApiProperty({ enum: ['active', 'invited', 'disabled'] })
  @Prop({ required: true, default: 'active', index: true })
  status!: UserStatus;
}

export const UserSchema = SchemaFactory.createForClass(User);
