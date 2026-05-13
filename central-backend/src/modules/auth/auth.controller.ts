import { Body, Controller, Headers, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { BootstrapAdminDto } from '../users/dto/bootstrap-admin.dto';
import { LoginDto } from '../users/dto/login.dto';
import { AuthService } from './auth.service';
import { RegisterDeviceDto } from './dto/register-device.dto';
import { DeviceLoginDto } from './dto/device-login.dto';

@ApiTags('auth')
@Controller('auth')
export class AuthController {
  constructor(private readonly authService: AuthService) {}

  @Post('login')
  async loginUser(@Body() dto: LoginDto): Promise<{ accessToken: string; user: Record<string, unknown> }> {
    return await this.authService.loginUser(dto);
  }

  @Post('bootstrap')
  async bootstrap(
    @Headers('x-auth-bootstrap-token') bootstrapToken: string | undefined,
    @Body() dto: BootstrapAdminDto,
  ): Promise<{ accessToken: string; user: Record<string, unknown> }> {
    return await this.authService.bootstrapAdmin(dto, bootstrapToken);
  }

  @Post('devices/register')
  async register(@Body() dto: RegisterDeviceDto) {
    const device = await this.authService.registerDevice(dto);
    return { storeId: device.storeId, deviceId: device.deviceId, isActive: device.isActive };
  }

  @Post('devices/login')
  async deviceLogin(@Body() dto: DeviceLoginDto) {
    const device = await this.authService.verifyDevice(dto.deviceId, dto.deviceSecret);
    return { storeId: device.storeId, deviceId: device.deviceId };
  }
}
