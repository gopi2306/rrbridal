import { PartialType } from '@nestjs/swagger';
import { CreateSellByTypeDto } from './create-sell-by-type.dto';

export class UpdateSellByTypeDto extends PartialType(CreateSellByTypeDto) {}
