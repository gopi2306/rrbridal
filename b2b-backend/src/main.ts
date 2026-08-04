import 'reflect-metadata';
import { ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { NestExpressApplication } from '@nestjs/platform-express';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { AppModule } from './app.module';

export async function bootstrap() {
  const app = await NestFactory.create<NestExpressApplication>(AppModule);
  app.setGlobalPrefix('api');
  app.useBodyParser('json', { limit: process.env.B2B_BODY_LIMIT ?? '2mb' });
  app.useBodyParser('urlencoded', { limit: process.env.B2B_BODY_LIMIT ?? '2mb', extended: true });
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
    }),
  );

  const origins = (process.env.B2B_CORS_ORIGINS ?? '')
    .split(',')
    .map((origin) => origin.trim())
    .filter(Boolean);
  app.enableCors({
    origin: origins.length ? origins : false,
    credentials: false,
    methods: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS'],
  });

  const swaggerConfig = new DocumentBuilder()
    .setTitle('RR Bridal Multi-Database B2B API')
    .setDescription('Internal API for database-wise inventory and B2B operations')
    .setVersion('1.0.0')
    .addApiKey({ type: 'apiKey', name: 'x-b2b-admin-key', in: 'header' }, 'management-key')
    .build();
  SwaggerModule.setup('api/swagger', app, SwaggerModule.createDocument(app, swaggerConfig));

  app.enableShutdownHooks();
  const port = Number(process.env.PORT ?? 3100);
  const host = process.env.HOST ?? '127.0.0.1';
  await app.listen(port, host);
  return app;
}

if (require.main === module) {
  bootstrap().catch((error) => {
    console.error('B2B backend failed to start', error);
    process.exitCode = 1;
  });
}
