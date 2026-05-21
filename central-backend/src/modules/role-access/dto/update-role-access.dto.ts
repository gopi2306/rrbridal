import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsIn, IsOptional } from 'class-validator';

const statuses = ['active', 'inactive'] as const;

export class UpdateRoleAccessDto {
  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  allow?: boolean;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];
}
