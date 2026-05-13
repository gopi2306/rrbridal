import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { JwtAuthModule } from '../auth/jwt-auth.module';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { StoresModule } from '../stores/stores.module';
import { AdminAuthSettingsController } from './admin-auth-settings.controller';
import { AdminUsersController } from './admin-users.controller';
import { AuthSettingsService } from './auth-settings.service';
import { RolesController } from './roles.controller';
import { StoreUsersController } from './store-users.controller';
import { AuthSettings, AuthSettingsSchema } from './schemas/auth-settings.schema';
import { RoleDefinition, RoleDefinitionSchema } from './schemas/role-definition.schema';
import { Store, StoreSchema } from '../stores/schemas/store.schema';
import { User, UserSchema } from './schemas/user.schema';
import { UsersSeedService } from './users-seed.service';
import { UsersService } from './users.service';

@Module({
  imports: [
    JwtAuthModule,
    StoresModule,
    MongooseModule.forFeature([
      { name: User.name, schema: UserSchema },
      { name: AuthSettings.name, schema: AuthSettingsSchema },
      { name: RoleDefinition.name, schema: RoleDefinitionSchema },
      { name: Store.name, schema: StoreSchema },
    ]),
  ],
  controllers: [AdminUsersController, AdminAuthSettingsController, RolesController, StoreUsersController],
  providers: [UsersService, AuthSettingsService, UsersSeedService, JwtAuthGuard, RolesGuard],
  exports: [UsersService],
})
export class UsersModule {}
