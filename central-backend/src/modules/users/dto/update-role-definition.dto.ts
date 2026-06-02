import { PartialType, OmitType } from '@nestjs/swagger';
import { CreateRoleDefinitionDto } from './create-role-definition.dto';

/** Role code is immutable after create. */
export class UpdateRoleDefinitionDto extends PartialType(
  OmitType(CreateRoleDefinitionDto, ['code'] as const),
) {}
