import { PartialType } from '@nestjs/swagger';
import { CreateBarcodeLabelDesignDto } from './create-barcode-label-design.dto';

export class UpdateBarcodeLabelDesignDto extends PartialType(CreateBarcodeLabelDesignDto) {}
