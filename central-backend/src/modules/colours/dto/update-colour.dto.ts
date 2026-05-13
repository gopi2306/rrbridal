import { PartialType } from '@nestjs/swagger';
import { CreateColourDto } from './create-colour.dto';

export class UpdateColourDto extends PartialType(CreateColourDto) {}
