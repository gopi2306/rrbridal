import { CanActivate, ExecutionContext, Inject, Injectable, ServiceUnavailableException, UnauthorizedException } from '@nestjs/common';
import { timingSafeEqual } from 'crypto';
import type { Request } from 'express';

export const ADMIN_API_KEY = Symbol('ADMIN_API_KEY');

@Injectable()
export class AdminApiKeyGuard implements CanActivate {
  constructor(@Inject(ADMIN_API_KEY) private readonly configuredKey: string) {}

  canActivate(context: ExecutionContext): boolean {
    if (this.configuredKey.length < 24) {
      throw new ServiceUnavailableException('Management API key is not configured');
    }
    const request = context.switchToHttp().getRequest<Request>();
    const supplied = request.header('x-b2b-admin-key') ?? '';
    const expectedBuffer = Buffer.from(this.configuredKey);
    const suppliedBuffer = Buffer.from(supplied);
    if (
      suppliedBuffer.length !== expectedBuffer.length ||
      !timingSafeEqual(suppliedBuffer, expectedBuffer)
    ) {
      throw new UnauthorizedException('Invalid management API key');
    }
    return true;
  }
}
