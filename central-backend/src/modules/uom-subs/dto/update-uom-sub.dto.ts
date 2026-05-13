import { PartialType } from '@nestjs/swagger';
import { CreateUomSubDto } from './create-uom-sub.dto';

export class UpdateUomSubDto extends PartialType(CreateUomSubDto) {}
