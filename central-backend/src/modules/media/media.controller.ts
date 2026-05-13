import { BadRequestException, Controller, Get, Param, Post, Query, Res, UploadedFile, UploadedFiles, UseInterceptors } from '@nestjs/common';
import { FileInterceptor, FilesInterceptor } from '@nestjs/platform-express';
import { ApiBody, ApiConsumes, ApiQuery, ApiTags } from '@nestjs/swagger';
import type { Response } from 'express';
import type { Request } from 'express';
import { diskStorage } from 'multer';
import { extname, join } from 'path';
import { existsSync, mkdirSync, readdirSync } from 'fs';
import { randomUUID } from 'crypto';
import { ProductsService } from '../products/products.service';

function ensureDir(dir: string) {
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
}

function generalDiskStorage(subfolder: string) {
  const base = join(process.cwd(), 'uploads', subfolder);
  ensureDir(base);
  return diskStorage({
    destination: (_req: Request, _file, cb) => cb(null, base),
    filename: (_req: Request, file, cb) => {
      const uniqueName = `${Date.now()}-${randomUUID()}${extname(file.originalname) || ''}`;
      cb(null, uniqueName);
    },
  });
}

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
const ALLOWED_FILE_TYPES = [
  ...ALLOWED_IMAGE_TYPES,
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'text/csv',
];

@ApiTags('media')
@Controller('media')
export class MediaController {
  private readonly productUploadRoot = join(process.cwd(), 'uploads', 'products');
  private readonly generalUploadRoot = join(process.cwd(), 'uploads', 'general');

  constructor(private readonly productsService: ProductsService) {
    ensureDir(this.productUploadRoot);
    ensureDir(this.generalUploadRoot);
  }

  // ── General File Upload (single) ──

  @Post('upload')
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      properties: {
        file: { type: 'string', format: 'binary' },
        folder: { type: 'string', description: 'Optional subfolder (e.g. suppliers, products)' },
      },
    },
  })
  @UseInterceptors(
    FileInterceptor('file', {
      storage: generalDiskStorage('general'),
      limits: { fileSize: 10 * 1024 * 1024 },
      fileFilter: (_req, file, cb) => {
        if (!ALLOWED_FILE_TYPES.includes(file.mimetype)) {
          return cb(new BadRequestException(`Unsupported file type: ${file.mimetype}`), false);
        }
        cb(null, true);
      },
    }),
  )
  async uploadSingle(
    @UploadedFile() file?: Express.Multer.File,
    @Query('folder') folder?: string,
  ) {
    if (!file) throw new BadRequestException('file is required');

    if (folder && folder.trim()) {
      const targetDir = join(process.cwd(), 'uploads', folder.trim());
      ensureDir(targetDir);
      const { renameSync } = require('fs');
      const newPath = join(targetDir, file.filename);
      renameSync(file.path, newPath);
      return {
        ok: true,
        filename: file.filename,
        originalName: file.originalname,
        mimetype: file.mimetype,
        size: file.size,
        url: `/media/files/${folder.trim()}/${file.filename}`,
      };
    }

    return {
      ok: true,
      filename: file.filename,
      originalName: file.originalname,
      mimetype: file.mimetype,
      size: file.size,
      url: `/media/files/general/${file.filename}`,
    };
  }

  // ── General File Upload (multiple) ──

  @Post('upload/multiple')
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      properties: {
        files: { type: 'array', items: { type: 'string', format: 'binary' } },
      },
    },
  })
  @UseInterceptors(
    FilesInterceptor('files', 10, {
      storage: generalDiskStorage('general'),
      limits: { fileSize: 10 * 1024 * 1024 },
      fileFilter: (_req, file, cb) => {
        if (!ALLOWED_FILE_TYPES.includes(file.mimetype)) {
          return cb(new BadRequestException(`Unsupported file type: ${file.mimetype}`), false);
        }
        cb(null, true);
      },
    }),
  )
  async uploadMultiple(
    @UploadedFiles() files?: Express.Multer.File[],
    @Query('folder') folder?: string,
  ) {
    if (!files?.length) throw new BadRequestException('At least one file is required');

    const subDir = folder?.trim() || 'general';
    if (subDir !== 'general') {
      const targetDir = join(process.cwd(), 'uploads', subDir);
      ensureDir(targetDir);
      const { renameSync } = require('fs');
      for (const f of files) {
        const newPath = join(targetDir, f.filename);
        renameSync(f.path, newPath);
      }
    }

    return {
      ok: true,
      count: files.length,
      files: files.map((f) => ({
        filename: f.filename,
        originalName: f.originalname,
        mimetype: f.mimetype,
        size: f.size,
        url: `/media/files/${subDir}/${f.filename}`,
      })),
    };
  }

  // ── Serve uploaded files ──

  @Get('files/:folder/:filename')
  async getFile(
    @Param('folder') folder: string,
    @Param('filename') filename: string,
    @Res() res: Response,
  ) {
    const filePath = join(process.cwd(), 'uploads', folder, filename);
    if (!existsSync(filePath)) return res.status(404).json({ message: 'File not found' });
    return res.sendFile(filePath);
  }

  // ── List files in a folder ──

  @Get('files/:folder')
  @ApiQuery({ name: 'folder', description: 'Subfolder name (e.g. general, suppliers, products)' })
  async listFiles(@Param('folder') folder: string) {
    const dir = join(process.cwd(), 'uploads', folder);
    if (!existsSync(dir)) return [];
    const files = readdirSync(dir).map((name) => ({
      filename: name,
      url: `/media/files/${folder}/${name}`,
    }));
    return files;
  }

  // ── Product Image Upload (existing) ──

  @Post('products/:productId/image')
  @ApiConsumes('multipart/form-data')
  @ApiBody({
    schema: {
      type: 'object',
      properties: {
        file: { type: 'string', format: 'binary' },
      },
    },
  })
  @UseInterceptors(
    FileInterceptor('file', {
      storage: diskStorage({
        destination: (_req: Request, _file, cb) => {
          cb(null, join(process.cwd(), 'uploads', 'products'));
        },
        filename: (req: Request, file, cb) => {
          const productId = (req.params as { productId?: string }).productId ?? 'unknown';
          cb(null, `${productId}${extname(file.originalname) || '.jpg'}`);
        },
      }),
      limits: { fileSize: 5 * 1024 * 1024 },
    }),
  )
  async uploadProductImage(@Param('productId') productId: string, @UploadedFile() file?: Express.Multer.File) {
    if (!file) throw new BadRequestException('file is required');
    await this.productsService.findById(productId);
    return { ok: true, filename: file.filename };
  }

  @Get('products/:productId/image')
  async getProductImage(@Param('productId') productId: string, @Res() res: Response) {
    const candidates = ['.jpg', '.jpeg', '.png', '.webp'].map((ext) => join(this.productUploadRoot, `${productId}${ext}`));
    const path = candidates.find((p) => existsSync(p));
    if (!path) return res.status(404).json({ message: 'Image not found' });
    return res.sendFile(path);
  }
}

