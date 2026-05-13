import { PartialType } from '@nestjs/swagger';
import { CreateIndentTypeDto } from './create-indent-type.dto';

export class UpdateIndentTypeDto extends PartialType(CreateIndentTypeDto) {}
