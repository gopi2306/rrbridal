import { OmitType } from '@nestjs/swagger';
import { InventoryFilteredQueryDto } from './inventory-filtered-query.dto';

export class InventoryFilteredSummaryQueryDto extends OmitType(InventoryFilteredQueryDto, [
  'page',
  'limit',
] as const) {}
