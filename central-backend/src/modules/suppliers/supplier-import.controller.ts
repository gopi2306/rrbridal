import {
  BadRequestException,
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
import { SupplierImportService } from './import/supplier-import.service';

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

function fileFilterCsvOrExcel(
  _req: Express.Request,
  file: Express.Multer.File,
  cb: (error: Error | null, accept: boolean) => void,
) {
  const name = file.originalname?.toLowerCase() ?? '';
  if (!name.endsWith('.xlsx') && !name.endsWith('.xls') && !name.endsWith('.csv')) {
    return cb(new BadRequestException('Only .csv, .xlsx, or .xls files are allowed'), false);
  }
  cb(null, true);
}

@ApiTags('suppliers-import')
@Controller('suppliers/import')
export class SupplierImportController {
  constructor(private readonly supplierImportService: SupplierImportService) {}

  @Get('excel/template')
  @ApiProduces('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')
  downloadExcelTemplate(@Res() res: Response): void {
    const buffer = this.supplierImportService.buildExcelTemplate();
    res.setHeader('Content-Type', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    res.setHeader('Content-Disposition', 'attachment; filename="supplier-import-template.xlsx"');
    res.send(buffer);
  }

  @Post('excel')
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      properties: { file: { type: 'string', format: 'binary' } },
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
    @Query('dryRun') dryRun?: string,
  ) {
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

    return await this.supplierImportService.runFromExcelBuffer(file.buffer, {
      dryRun: dryRun === 'true' || dryRun === '1',
    });
  }

  @Post()
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      properties: { file: { type: 'string', format: 'binary' } },
    },
  })
  @ApiQuery({ name: 'dryRun', required: false, type: Boolean })
  @UseInterceptors(
    FileInterceptor('file', {
      storage: memoryStorage(),
      limits: { fileSize: IMPORT_MAX_BYTES },
      fileFilter: fileFilterCsvOrExcel,
    }),
  )
  async importFile(
    @UploadedFile() file: Express.Multer.File | undefined,
    @Query('dryRun') dryRun?: string,
  ) {
    if (!file?.buffer?.length) {
      throw new BadRequestException('file is required');
    }

    return await this.supplierImportService.runFromBuffer(file.buffer, file.originalname ?? 'upload.xlsx', {
      dryRun: dryRun === 'true' || dryRun === '1',
    });
  }
}
