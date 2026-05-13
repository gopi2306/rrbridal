import { PartialType } from '@nestjs/swagger';
import { CreatePoQtyPolicyDto } from './create-po-qty-policy.dto';

export class UpdatePoQtyPolicyDto extends PartialType(CreatePoQtyPolicyDto) {}
