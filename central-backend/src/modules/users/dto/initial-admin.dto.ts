import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsNotEmpty, IsString, MinLength } from 'class-validator';

/** Body for `POST /admin/onboarding/initial-admin` (super_admin only, once per tenant). */
export class InitialAdminDto {
  @ApiProperty()
  @IsEmail()
  @IsNotEmpty()
  email!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @MinLength(8)
  password!: string;

  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  name!: string;
}
