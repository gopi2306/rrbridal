import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { AdminCompanyProfileController } from './admin-company-profile.controller';
import { CompanyProfileService } from './company-profile.service';
import { CompanyProfile, CompanyProfileSchema } from './schemas/company-profile.schema';

@Module({
  imports: [JwtAuthModule, MongooseModule.forFeature([{ name: CompanyProfile.name, schema: CompanyProfileSchema }])],
  controllers: [AdminCompanyProfileController],
  providers: [CompanyProfileService, JwtAuthGuard, RolesGuard],
  exports: [CompanyProfileService],
})
export class CompanyProfileModule {}
