import { Module, forwardRef } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { ResourceLimitsModule } from '../resource-limits/resource-limits.module';
import { Store, StoreSchema } from './schemas/store.schema';
import { AdminStoresController } from './admin-stores.controller';
import { StoresController } from './stores.controller';
import { StoresService } from './stores.service';

@Module({
  imports: [
    JwtAuthModule,
    forwardRef(() => ResourceLimitsModule),
    MongooseModule.forFeature([{ name: Store.name, schema: StoreSchema }]),
  ],
  controllers: [AdminStoresController, StoresController],
  providers: [StoresService, JwtAuthGuard],
  exports: [StoresService],
})
export class StoresModule {}
