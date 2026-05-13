import {
  BadRequestException,
  ConflictException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcryptjs';
import { Model } from 'mongoose';
import { JwtPayload } from '../../common/jwt-payload';
import { StoresService } from '../stores/stores.service';
import { BootstrapAdminDto } from '../users/dto/bootstrap-admin.dto';
import { LoginDto } from '../users/dto/login.dto';
import { UsersService } from '../users/users.service';
import { RegisterDeviceDto } from './dto/register-device.dto';
import { Device, DeviceDocument } from './schemas/device.schema';

@Injectable()
export class AuthService {
  constructor(
    @InjectModel(Device.name) private readonly deviceModel: Model<DeviceDocument>,
    private readonly usersService: UsersService,
    private readonly storesService: StoresService,
    private readonly jwtService: JwtService,
  ) {}

  async registerDevice(dto: RegisterDeviceDto) {
    const exists = await this.storesService.existsByCode(dto.storeId);
    if (!exists) throw new BadRequestException(`Unknown storeId '${dto.storeId}'`);
    const existing = await this.deviceModel.findOne({ deviceId: dto.deviceId }).lean();
    if (existing) return existing;
    return await this.deviceModel.create({
      storeId: dto.storeId,
      deviceId: dto.deviceId,
      deviceSecret: dto.deviceSecret,
      isActive: true,
    });
  }

  async verifyDevice(deviceId: string, deviceSecret: string) {
    const device = await this.deviceModel.findOne({ deviceId, isActive: true }).lean();
    if (!device) throw new UnauthorizedException('Invalid device');
    if (device.deviceSecret !== deviceSecret) throw new UnauthorizedException('Invalid device');
    return device;
  }

  async loginUser(dto: LoginDto): Promise<{ accessToken: string; user: Record<string, unknown> }> {
    const user = await this.usersService.findForLogin(dto.email);
    if (!user || user.status !== 'active') {
      throw new UnauthorizedException('Invalid credentials or account is not active');
    }
    const hash = user.passwordHash;
    if (!hash || !(await bcrypt.compare(dto.password, hash))) {
      throw new UnauthorizedException('Invalid credentials');
    }
    const accessToken = this.signUserJwt(user._id, user.email, user.role, user.locationKind, user.storeId);
    const { passwordHash: _p, ...safe } = user as typeof user & { passwordHash: string };
    return { accessToken, user: safe } as { accessToken: string; user: Record<string, unknown> };
  }

  async bootstrapAdmin(
    dto: BootstrapAdminDto,
    token: string | undefined,
  ): Promise<{ accessToken: string; user: Record<string, unknown> }> {
    const count = await this.usersService.countAll();
    if (count > 0) {
      throw new ConflictException('Bootstrap is only allowed when no users exist');
    }
    const expected = process.env.AUTH_BOOTSTRAP_TOKEN?.trim();
    if (!expected || (token?.trim() ?? '') !== expected) {
      throw new UnauthorizedException('Invalid or missing X-Auth-Bootstrap-Token');
    }
    const user = await this.usersService.createBootstrapAdmin(dto);
    const accessToken = this.signUserJwt(user._id, user.email, user.role, user.locationKind, user.storeId);
    return { accessToken, user } as { accessToken: string; user: Record<string, unknown> };
  }

  private signUserJwt(
    _id: unknown,
    email: string,
    role: string,
    locationKind: string,
    storeId?: string,
  ) {
    const payload: JwtPayload = {
      sub: String(_id),
      email,
      role,
      locationKind,
    };
    if (storeId !== undefined && storeId !== '') payload.storeId = storeId;
    return this.jwtService.sign(payload);
  }
}
