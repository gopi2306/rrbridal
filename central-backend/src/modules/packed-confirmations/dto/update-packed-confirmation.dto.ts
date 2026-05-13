import { PartialType } from '@nestjs/swagger';
import { CreatePackedConfirmationDto } from './create-packed-confirmation.dto';

export class UpdatePackedConfirmationDto extends PartialType(CreatePackedConfirmationDto) {}
