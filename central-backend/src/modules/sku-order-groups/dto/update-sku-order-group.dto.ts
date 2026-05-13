import { PartialType } from '@nestjs/swagger';
import { CreateSkuOrderGroupDto } from './create-sku-order-group.dto';

export class UpdateSkuOrderGroupDto extends PartialType(CreateSkuOrderGroupDto) {}
