import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { AdminRoleAccessController } from './admin-role-access.controller';
import { RoleAccess, RoleAccessSchema } from './schemas/role-access.schema';
import { RoleAccessService } from './role-access.service';

@Module({
  imports: [
    JwtAuthModule,
    MongooseModule.forFeature([{ name: RoleAccess.name, schema: RoleAccessSchema }]),
  ],
  controllers: [AdminRoleAccessController],
  providers: [RoleAccessService, JwtAuthGuard],
  exports: [RoleAccessService],
})
export class RoleAccessModule {}
