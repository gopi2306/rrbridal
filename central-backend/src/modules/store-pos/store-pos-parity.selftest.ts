import assert from 'node:assert/strict';
import { StorePosService } from './store-pos.service';

async function run() {
  const calls: { storeId?: string; limit?: number; event?: Record<string, unknown> } = {};
  const syncService = {
    applyOne: async (event: Record<string, unknown>) => {
      calls.event = event;
      return { eventId: String(event.eventId), status: 'applied' as const };
    },
  };
  const storesService = {
    existsByCode: async (storeId: string) => storeId === 'store-001',
  };
  const stockTransfersService = {
    listAwaitingIntakeForStore: async (storeId: string, limit: number) => {
      calls.storeId = storeId;
      calls.limit = limit;
      return [
        { _id: 'transfer-out', direction: 'store_to_warehouse', transferNo: 'TR-OUT' },
        { _id: 'transfer-in', direction: 'warehouse_to_store', transferNo: 'TR-IN' },
      ];
    },
    toSyncTransferPayload: (transfer: Record<string, unknown>) => ({
      transferId: transfer._id,
      transferNo: transfer.transferNo,
      direction: transfer.direction,
      status: 'awaiting_intake',
      lines: [{ sku: 'SKU-1', qty: 2 }],
    }),
  };

  const service = new StorePosService(
    syncService as never,
    {} as never,
    {} as never,
    storesService as never,
    stockTransfersService as never,
  );

  const pending = await service.listAwaitingTransfers(' store-001 ', 25);
  assert.equal(calls.storeId, 'store-001');
  assert.equal(calls.limit, 25);
  assert.equal(pending.length, 2);
  assert.equal(pending[0]?.direction, 'store_to_warehouse');
  assert.equal(pending[1]?.direction, 'warehouse_to_store');

  await service.applyEvent({
    type: 'StockTransferReceived',
    storeId: 'store-001',
    deviceId: 'POS-1',
    eventId: 'stock-transfer-received:store-001:transfer-out',
    payload: {
      transferId: 'transfer-out',
      lines: [{ sku: 'SKU-1', qty: 2 }],
    },
  });
  assert.equal(calls.event?.eventId, 'stock-transfer-received:store-001:transfer-out');
  assert.equal(calls.event?.type, 'StockTransferReceived');
}

void run()
  .then(() => console.log('store-pos parity self-test passed'))
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
