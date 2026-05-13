import { PartialType } from '@nestjs/swagger';
import { CreateHsnCodeDto } from './create-hsn-code.dto';

export class UpdateHsnCodeDto extends PartialType(CreateHsnCodeDto) {}
