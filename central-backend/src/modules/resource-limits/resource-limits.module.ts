import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { AuthSettingsService } from '../users/auth-settings.service';
import { AuthSettings, AuthSettingsSchema } from '../users/schemas/auth-settings.schema';
import { User, UserSchema } from '../users/schemas/user.schema';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { Location, LocationSchema } from '../locations/schemas/location.schema';
import { Product, ProductSchema } from '../products/schemas/product.schema';
import { ResourceLimits, ResourceLimitsSchema } from './schemas/resource-limits.schema';
import { ResourceLimitsController } from './resource-limits.controller';
import { ResourceLimitsService } from './resource-limits.service';

@Module({
  imports: [
    JwtAuthModule,
    MongooseModule.forFeature([
      { name: ResourceLimits.name, schema: ResourceLimitsSchema },
      { name: User.name, schema: UserSchema },
      { name: Store.name, schema: StoreSchema },
      { name: AuthSettings.name, schema: AuthSettingsSchema },
      { name: Location.name, schema: LocationSchema },
      { name: Product.name, schema: ProductSchema },
    ]),
  ],
  controllers: [ResourceLimitsController],
  providers: [ResourceLimitsService, AuthSettingsService, JwtAuthGuard, RolesGuard],
  exports: [ResourceLimitsService],
})
export class ResourceLimitsModule {}
