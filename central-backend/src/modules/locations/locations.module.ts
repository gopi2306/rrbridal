import { Module, forwardRef } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { ResourceLimitsModule } from '../resource-limits/resource-limits.module';
import { Location, LocationSchema } from './schemas/location.schema';
import { LocationsController } from './locations.controller';
import { LocationsService } from './locations.service';

@Module({
  imports: [
    JwtAuthModule,
    forwardRef(() => ResourceLimitsModule),
    MongooseModule.forFeature([{ name: Location.name, schema: LocationSchema }]),
  ],
  controllers: [LocationsController],
  providers: [LocationsService, JwtAuthGuard, RolesGuard],
  exports: [LocationsService],
})
export class LocationsModule {}
