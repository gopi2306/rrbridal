import { ApiProperty } from '@nestjs/swagger';
import { IsObject } from 'class-validator';

export class PatchAuthSettingsDto {
  @ApiProperty({
    description: 'Partial map of role code to max user count',
    example: { warehouse: 1, store: 5 },
  })
  @IsObject()
  roleQuotas!: Record<string, number>;
}
