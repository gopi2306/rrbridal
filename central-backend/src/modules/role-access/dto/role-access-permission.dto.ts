import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsNotEmpty, IsString } from 'class-validator';

export class RoleAccessPermissionDto {
  @ApiProperty({ example: 'core' })
  @IsString()
  @IsNotEmpty()
  area!: string;

  @ApiProperty({ example: 'Dashboard' })
  @IsString()
  @IsNotEmpty()
  screen!: string;

  @ApiProperty({ example: true })
  @IsBoolean()
  allow!: boolean;
}
