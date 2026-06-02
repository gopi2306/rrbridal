import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsInt, IsNotEmpty, IsOptional, IsString, Matches, MaxLength, Min } from 'class-validator';

export class CreateRoleDefinitionDto {
  @ApiProperty({ example: 'warehouse', description: 'Unique role code (lowercase, underscores)' })
  @IsString()
  @IsNotEmpty()
  @MaxLength(48)
  @Matches(/^[a-z][a-z0-9_]*$/, {
    message: 'code must start with a letter and contain only lowercase letters, digits, and underscores',
  })
  code!: string;

  @ApiProperty({ example: 'Warehouse Manager' })
  @IsString()
  @IsNotEmpty()
  @MaxLength(120)
  displayName!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  description?: string;

  @ApiProperty({ required: false, default: 0 })
  @IsInt()
  @Min(0)
  @IsOptional()
  sortOrder?: number;

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;
}
