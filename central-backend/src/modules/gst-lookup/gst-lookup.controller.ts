import { Controller, Get, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { GstLookupService } from './gst-lookup.service';

@ApiTags('gst')
@Controller('gst')
export class GstLookupController {
  constructor(private readonly gstLookup: GstLookupService) {}

  @Get('lookup')
  @ApiQuery({ name: 'gstin', required: true })
  async lookup(@Query('gstin') gstin?: string) {
    return await this.gstLookup.lookup(gstin);
  }
}
