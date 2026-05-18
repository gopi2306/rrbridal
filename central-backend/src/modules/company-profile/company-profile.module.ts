import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { AdminCompanyProfileController } from './admin-company-profile.controller';
import { CompanyProfileController } from './company-profile.controller';
import { CompanyProfileSeedService } from './company-profile-seed.service';
import { CompanyProfileService } from './company-profile.service';
import { CompanyProfile, CompanyProfileSchema } from './schemas/company-profile.schema';

@Module({
  imports: [
    JwtAuthModule,
    MongooseModule.forFeature([
      { name: CompanyProfile.name, schema: CompanyProfileSchema },
      { name: Store.name, schema: StoreSchema },
    ]),
  ],
  controllers: [AdminCompanyProfileController, CompanyProfileController],
  providers: [CompanyProfileService, CompanyProfileSeedService, JwtAuthGuard, RolesGuard],
  exports: [CompanyProfileService],
})
export class CompanyProfileModule {}
