import { PartialType } from '@nestjs/swagger';
import { CreateWeightUnitDto } from './create-weight-unit.dto';

export class UpdateWeightUnitDto extends PartialType(CreateWeightUnitDto) {}
