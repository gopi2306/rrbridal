import { PartialType } from '@nestjs/swagger';
import { CreateSkuTypeDto } from './create-sku-type.dto';

export class UpdateSkuTypeDto extends PartialType(CreateSkuTypeDto) {}
