import 'reflect-metadata';
import { ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { existsSync } from 'fs';
import { extname, join } from 'path';
import { AppModule } from './modules/app/app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);

  app.setGlobalPrefix('api', {
    exclude: ['/'],
  });

  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
    }),
  );

  const swaggerConfig = new DocumentBuilder()
    .setTitle('RR Bridal Central API')
    .setDescription('Central warehouse + sync API for stores')
    .setVersion('0.1.0')
    .addBearerAuth()
    .build();

  const document = SwaggerModule.createDocument(app, swaggerConfig);
  SwaggerModule.setup('api/swagger', app, document);

  // Legacy upload URLs omitted the /api prefix; keep old paths working for stored companyLogo values.
  const expressApp = app.getHttpAdapter().getInstance();
  expressApp.get('/media/files/:folder/:filename', (req: { params: { folder: string; filename: string } }, res: {
    redirect: (code: number, url: string) => void;
    status: (code: number) => { json: (body: unknown) => void };
    type: (mime: string) => void;
    sendFile: (path: string) => void;
  }) => {
    const { folder, filename } = req.params;
    const filePath = join(process.cwd(), 'uploads', folder, filename);
    if (!existsSync(filePath)) {
      res.status(404).json({ message: 'File not found' });
      return;
    }
    const ext = extname(filename).toLowerCase();
    const mime =
      ext === '.png'
        ? 'image/png'
        : ext === '.jpg' || ext === '.jpeg'
          ? 'image/jpeg'
          : ext === '.webp'
            ? 'image/webp'
            : ext === '.gif'
              ? 'image/gif'
              : undefined;
    if (mime) res.type(mime);
    res.sendFile(filePath);
  });

  const port = process.env.PORT ? Number(process.env.PORT) : 3000;
  await app.listen(port);
}

bootstrap().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exitCode = 1;
});

