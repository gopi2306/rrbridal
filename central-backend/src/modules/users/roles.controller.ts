import { Controller, Get } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { RoleDefinition, RoleDefinitionDocument } from './schemas/role-definition.schema';

@ApiTags('roles')
@Controller('roles')
export class RolesController {
  constructor(@InjectModel(RoleDefinition.name) private readonly roleModel: Model<RoleDefinitionDocument>) {}

  @Get()
  async list() {
    return await this.roleModel.find().sort({ sortOrder: 1 }).lean();
  }
}
