import { PartialType } from '@nestjs/swagger';
import { CreateColourTypeDto } from './create-colour-type.dto';

export class UpdateColourTypeDto extends PartialType(CreateColourTypeDto) {}
