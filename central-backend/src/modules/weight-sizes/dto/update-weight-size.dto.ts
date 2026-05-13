import { PartialType } from '@nestjs/swagger';
import { CreateWeightSizeDto } from './create-weight-size.dto';

export class UpdateWeightSizeDto extends PartialType(CreateWeightSizeDto) {}
