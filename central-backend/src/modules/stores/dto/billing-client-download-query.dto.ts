import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, Max, Min } from 'class-validator';

export class BillingClientDownloadQueryDto {
  @ApiProperty({ example: 1, minimum: 1, maximum: 99, description: 'POS counter number (1 = manager till)' })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(99)
  posCounter!: number;

  @ApiPropertyOptional({
    enum: ['exe', 'zip'],
    default: 'zip',
    description:
      'zip = full self-contained folder + .env. exe = tiny zip with PublishSingleFile EXE + .env only.',
  })
  @IsOptional()
  @IsIn(['exe', 'zip'])
  format?: 'exe' | 'zip';
}
