import { PartialType } from '@nestjs/swagger';
import { CreateBatchSelectionDto } from './create-batch-selection.dto';

export class UpdateBatchSelectionDto extends PartialType(CreateBatchSelectionDto) {}
