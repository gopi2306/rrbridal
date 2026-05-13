import { PartialType } from '@nestjs/swagger';
import { CreateGstUomDto } from './create-gst-uom.dto';

export class UpdateGstUomDto extends PartialType(CreateGstUomDto) {}
