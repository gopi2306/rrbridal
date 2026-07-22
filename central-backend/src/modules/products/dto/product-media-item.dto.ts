import { ApiProperty } from '@nestjs/swagger';
import { IsArray, IsNotEmpty, IsOptional, IsString } from 'class-validator';

export class ProductMediaItemDto {
  @ApiProperty({
    description: 'Public URL of the product image/file',
    example: '/api/media/files/products/507f1f77bcf86cd799439011.jpg',
  })
  @IsString()
  @IsNotEmpty()
  url!: string;

  @ApiProperty({ required: false, description: 'Optional description for this image' })
  @IsString()
  @IsOptional()
  description?: string;

  @ApiProperty({
    required: false,
    type: [String],
    description: 'Optional colour ids shown in this image',
    example: ['507f1f77bcf86cd799439011'],
  })
  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  colourIds?: string[];
}
