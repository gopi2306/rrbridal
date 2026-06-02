import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsOptional, IsString, MinLength } from 'class-validator';

/** Body for `PATCH /admin/onboarding/initial-admin` (super_admin only). */
export class UpdateInitialAdminDto {
  @ApiProperty({ required: false })
  @IsEmail()
  @IsOptional()
  email?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty({ required: false })
  @IsString()
  @MinLength(8)
  @IsOptional()
  password?: string;
}

