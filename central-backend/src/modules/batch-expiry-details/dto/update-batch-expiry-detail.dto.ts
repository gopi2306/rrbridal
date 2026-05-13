import { PartialType } from '@nestjs/swagger';
import { CreateBatchExpiryDetailDto } from './create-batch-expiry-detail.dto';

export class UpdateBatchExpiryDetailDto extends PartialType(CreateBatchExpiryDetailDto) {}
