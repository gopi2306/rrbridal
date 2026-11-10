import { CallHandler, ExecutionContext, Injectable, NestInterceptor } from '@nestjs/common';
import { Observable, tap } from 'rxjs';
import { buildMutationAudit } from './audit-request.util';
import { AuditLogsService } from './audit-logs.service';

type AuditHttpRequest = {
  method?: string;
  originalUrl?: string;
  url?: string;
  params?: Record<string, unknown>;
  body?: unknown;
  user?: { sub?: string; email?: string; role?: string; storeId?: string };
};

@Injectable()
export class AuditMutationInterceptor implements NestInterceptor {
  constructor(private readonly auditLogs: AuditLogsService) {}

  intercept(context: ExecutionContext, next: CallHandler): Observable<unknown> {
    if (context.getType() !== 'http') return next.handle();
    const http = context.switchToHttp();
    const req = http.getRequest<AuditHttpRequest>();
    const method = req.method ?? 'GET';
    const url = req.originalUrl ?? req.url ?? '/';
    if (!buildMutationAudit({ method, url })) return next.handle();

    return next.handle().pipe(
      tap(() => {
        const res = http.getResponse<{ statusCode?: number }>();
        const draft = buildMutationAudit({
          method,
          url,
          ...(typeof res?.statusCode === 'number' ? { statusCode: res.statusCode } : {}),
          ...(req.params ? { params: req.params } : {}),
          ...(req.body !== undefined ? { body: req.body } : {}),
          ...(req.user ? { user: req.user } : {}),
        });
        if (!draft) return;
        void this.auditLogs
          .logEvent({
            entityType: draft.entityType,
            entityId: draft.entityId,
            action: draft.action,
            metadata: draft.metadata,
            source: draft.source,
            ...(draft.actor ? { actor: draft.actor } : {}),
            ...(draft.storeId ? { storeId: draft.storeId } : {}),
            ...(draft.deviceId ? { deviceId: draft.deviceId } : {}),
            ...(draft.billNo ? { billNo: draft.billNo } : {}),
            ...(draft.posCounter ? { posCounter: draft.posCounter } : {}),
          })
          .catch(() => undefined);
      }),
    );
  }
}
