import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type ResourceLimitsDocument = HydratedDocument<ResourceLimits>;

export const RESOURCE_LIMITS_KEY = 'default';

@Schema({ timestamps: true, collection: 'resource_limits' })
export class ResourceLimits {
  @ApiProperty({ description: 'Singleton key' })
  @Prop({ required: true, unique: true, default: RESOURCE_LIMITS_KEY })
  settingsKey!: string;

  @ApiProperty({ description: 'Maximum number of active stores allowed' })
  @Prop({ required: true, default: 3 })
  maxStores!: number;

  @ApiProperty({ description: 'Maximum number of warehouse locations allowed' })
  @Prop({ required: true, default: 5 })
  maxWarehouses!: number;

  @ApiProperty({ description: 'Maximum users assigned to a single store (role store, active/invited)' })
  @Prop({ required: true, default: 20 })
  maxUsersPerStore!: number;

  @ApiProperty({
    description:
      'Maximum warehouse users per warehouse location (role warehouse, locationKind warehouse, active/invited)',
  })
  @Prop({ required: true, default: 20 })
  maxUsersPerWarehouse!: number;
}

export const ResourceLimitsSchema = SchemaFactory.createForClass(ResourceLimits);
