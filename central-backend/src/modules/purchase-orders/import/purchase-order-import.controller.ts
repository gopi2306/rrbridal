import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Post,
  Query,
  Res,
  UploadedFile,
  UseInterceptors,
} from '@nestjs/common';
import { FileInterceptor } from '@nestjs/platform-express';
import { ApiBody, ApiConsumes, ApiProduces, ApiQuery, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import { memoryStorage } from 'multer';
import { PurchaseOrderImportService } from './purchase-order-import.service';
import type { PurchaseOrderImportOptions } from './purchase-order-import.types';

const IMPORT_MAX_BYTES = 10 * 1024 * 1024;
const EXCEL_MIMES = new Set([
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.ms-excel',
]);

function fileFilterExcel(
  _req: Express.Request,
  file: Express.Multer.File,
  cb: (error: Error | null, accept: boolean) => void,
) {
  const name = file.originalname?.toLowerCase() ?? '';
  if (!name.endsWith('.xlsx') && !name.endsWith('.xls')) {
    return cb(new BadRequestException('Only .xlsx or .xls files are allowed'), false);
  }
  cb(null, true);
}

type ImportFormBody = {
  supplierId?: string;
  supplierName?: string;
  mainLocationId?: string;
  branchId?: string;
  mainDivisionId?: string;
};

@ApiTags('purchase-orders-import')
@Controller('purchase-orders/import')
export class PurchaseOrderImportController {
  constructor(private readonly importService: PurchaseOrderImportService) {}

  @Get('excel/template')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  downloadExcelTemplate(@Res() res: Response): void {
    const buffer = this.importService.buildExcelTemplate();
    res.setHeader('Content-Type', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    res.setHeader('Content-Disposition', 'attachment; filename="purchase-order-import-template.xlsx"');
    res.send(buffer);
  }

  @Post('excel')
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      required: ['file', 'supplierId'],
      properties: {
        file: { type: 'string', format: 'binary' },
        supplierId: { type: 'string' },
        supplierName: { type: 'string' },
        mainLocationId: { type: 'string' },
        branchId: { type: 'string' },
        mainDivisionId: { type: 'string' },
      },
    },
  })
  @ApiQuery({ name: 'dryRun', required: false, type: Boolean })
  @UseInterceptors(
    FileInterceptor('file', {
      storage: memoryStorage(),
      limits: { fileSize: IMPORT_MAX_BYTES },
      fileFilter: fileFilterExcel,
    }),
  )
  async importExcel(
    @UploadedFile() file: Express.Multer.File | undefined,
    @Body() body: ImportFormBody,
    @Query('dryRun') dryRun?: string,
  ) {
    this.assertExcelFile(file);
    return await this.importService.importPoOnly(file!.buffer, this.toOptions(body, dryRun));
  }

  @Post('excel/receive')
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      required: ['file', 'supplierId'],
      properties: {
        file: { type: 'string', format: 'binary' },
        supplierId: { type: 'string' },
        supplierName: { type: 'string' },
        mainLocationId: { type: 'string' },
        branchId: { type: 'string' },
        mainDivisionId: { type: 'string' },
      },
    },
  })
  @ApiQuery({ name: 'dryRun', required: false, type: Boolean })
  @UseInterceptors(
    FileInterceptor('file', {
      storage: memoryStorage(),
      limits: { fileSize: IMPORT_MAX_BYTES },
      fileFilter: fileFilterExcel,
    }),
  )
  async importExcelReceive(
    @UploadedFile() file: Express.Multer.File | undefined,
    @Body() body: ImportFormBody,
    @Query('dryRun') dryRun?: string,
  ) {
    this.assertExcelFile(file);
    return await this.importService.importReceiveAndPost(file!.buffer, this.toOptions(body, dryRun));
  }

  private toOptions(body: ImportFormBody, dryRun?: string): PurchaseOrderImportOptions {
    const supplierId = body.supplierId?.trim() ?? '';
    if (!supplierId) {
      throw new BadRequestException('supplierId is required');
    }
    const options: PurchaseOrderImportOptions = {
      supplierId,
      dryRun: dryRun === 'true' || dryRun === '1',
    };
    if (body.supplierName?.trim()) options.supplierName = body.supplierName.trim();
    if (body.mainLocationId?.trim()) options.mainLocationId = body.mainLocationId.trim();
    if (body.branchId?.trim()) options.branchId = body.branchId.trim();
    if (body.mainDivisionId?.trim()) options.mainDivisionId = body.mainDivisionId.trim();
    return options;
  }

  private assertExcelFile(file: Express.Multer.File | undefined): void {
    if (!file?.buffer?.length) {
      throw new BadRequestException('file is required');
    }
    const name = file.originalname?.toLowerCase() ?? '';
    if (!name.endsWith('.xlsx') && !name.endsWith('.xls')) {
      throw new BadRequestException('Only Excel (.xlsx) files are accepted on this endpoint');
    }
    if (file.mimetype && !EXCEL_MIMES.has(file.mimetype) && file.mimetype !== 'application/octet-stream') {
      throw new BadRequestException(`Unsupported MIME type: ${file.mimetype}`);
    }
  }
}
