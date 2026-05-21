import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsIn, IsNotEmpty, IsOptional, IsString } from 'class-validator';

const statuses = ['active', 'inactive'] as const;

export class CreateRoleAccessDto {
  @ApiProperty({ example: 'admin' })
  @IsString()
  @IsNotEmpty()
  role!: string;

  @ApiProperty({ example: 'core' })
  @IsString()
  @IsNotEmpty()
  area!: string;

  @ApiProperty({ example: 'Dashboard' })
  @IsString()
  @IsNotEmpty()
  screen!: string;

  @ApiProperty({ required: false, default: false })
  @IsBoolean()
  @IsOptional()
  allow?: boolean;

  @ApiProperty({ required: false, enum: statuses, default: 'active' })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];
}
