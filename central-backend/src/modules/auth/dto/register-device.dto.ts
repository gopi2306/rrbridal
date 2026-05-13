import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString } from 'class-validator';

export class RegisterDeviceDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  @IsNotEmpty()
  storeId!: string;

  @ApiProperty({ example: 'device-001' })
  @IsString()
  @IsNotEmpty()
  deviceId!: string;

  @ApiProperty({ example: 'super-secret' })
  @IsString()
  @IsNotEmpty()
  deviceSecret!: string;
}

