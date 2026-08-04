import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean } from 'class-validator';

export class UpdateProductB2BVisibilityDto {
  @ApiProperty({
    description: 'Publish or hide this product in the B2B storefront',
    example: true,
  })
  @IsBoolean()
  isAddedInB2B!: boolean;
}
