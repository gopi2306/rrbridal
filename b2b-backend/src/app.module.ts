import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { BusinessController } from './business/business.controller';
import { BusinessService } from './business/business.service';
import { ConnectionRegistryService } from './database/connection-registry.service';
import { ADMIN_API_KEY, AdminApiKeyGuard } from './security/admin-api-key.guard';
import { StorefrontController } from './storefront/storefront.controller';
import { StorefrontService } from './storefront/storefront.service';

@Module({
  imports: [ConfigModule.forRoot({ isGlobal: true })],
  controllers: [StorefrontController, BusinessController],
  providers: [
    ConnectionRegistryService,
    BusinessService,
    StorefrontService,
    AdminApiKeyGuard,
    {
      provide: ADMIN_API_KEY,
      useFactory: () => {
        const key = process.env.B2B_ADMIN_API_KEY?.trim() ?? '';
        if (key.length < 24) {
          throw new Error('B2B_ADMIN_API_KEY must contain at least 24 characters');
        }
        return key;
      },
    },
  ],
})
export class AppModule {}
