import { IsOptional, IsString, Matches } from 'class-validator';

export class StoreDayCloseDashboardQueryDto {
  @IsOptional()
  @IsString()
  storeId?: string;

  @IsOptional()
  @IsString()
  @Matches(/^\d{4}-\d{2}-\d{2}$/)
  businessDate?: string;

  @IsOptional()
  @IsString()
  posCounter?: string;
}
