import { Module } from '@nestjs/common';
import { MediaController } from './media.controller';
import { ProductsModule } from '../products/products.module';

@Module({
  imports: [ProductsModule],
  controllers: [MediaController],
})
export class MediaModule {}

