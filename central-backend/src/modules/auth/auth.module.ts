import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { StoresModule } from '../stores/stores.module';
import { UsersModule } from '../users/users.module';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import { JwtAuthModule } from './jwt-auth.module';
import { Device, DeviceSchema } from './schemas/device.schema';

@Module({
  imports: [
    JwtAuthModule,
    MongooseModule.forFeature([{ name: Device.name, schema: DeviceSchema }]),
    UsersModule,
    StoresModule,
  ],
  controllers: [AuthController],
  providers: [AuthService],
  exports: [AuthService, JwtAuthModule],
})
export class AuthModule {}
