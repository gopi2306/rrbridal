import { PartialType } from '@nestjs/swagger';
import { CreateItemPrepStatusDto } from './create-item-prep-status.dto';

export class UpdateItemPrepStatusDto extends PartialType(CreateItemPrepStatusDto) {}
