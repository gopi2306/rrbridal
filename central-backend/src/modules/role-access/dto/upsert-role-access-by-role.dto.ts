import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ArrayMinSize, IsArray, ValidateNested } from 'class-validator';
import { RoleAccessPermissionDto } from './role-access-permission.dto';

export class UpsertRoleAccessByRoleDto {
  @ApiProperty({ type: [RoleAccessPermissionDto] })
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => RoleAccessPermissionDto)
  permissions!: RoleAccessPermissionDto[];
}
