import { PartialType } from '@nestjs/swagger';
import { CreateOfferGroupDto } from './create-offer-group.dto';

export class UpdateOfferGroupDto extends PartialType(CreateOfferGroupDto) {}
