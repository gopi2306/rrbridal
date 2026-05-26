import { ApiProperty } from '@nestjs/swagger';
import { Transform } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, MaxLength, Min, MinLength } from 'class-validator';

const undefinedIfNull = ({ value }: { value: unknown }) => (value === null ? undefined : value);

export class PatchDocumentNumberConfigDto {
  @ApiProperty({
    required: false,
    example: 'PO-',
    description: 'Literal prefix before the padded number. Use "" for numbers only (e.g. 100001).',
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsString()
  @MinLength(0)
  @MaxLength(20)
  prefix?: string;

  @ApiProperty({ required: false, minimum: 1, maximum: 10 })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsInt()
  @Min(1)
  @Max(10)
  padLength?: number;

  @ApiProperty({
    required: false,
    description:
      'Numeric suffix for the next issued document (e.g. 100001 → PO-100001 with prefix, or 100001 alone when prefix is empty)',
    example: 100001,
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsInt()
  @Min(1)
  startFrom?: number;

  @ApiProperty({ required: false })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsString()
  @MaxLength(80)
  label?: string;

  @ApiProperty({ required: false })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsString()
  @MaxLength(500)
  description?: string;
}
