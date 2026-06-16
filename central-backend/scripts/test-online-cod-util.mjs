import {
  isOnlineCodBill,
  isOnlineCodPending,
  parseOnlineCodAmount,
  parseOnlineCodTransactionNo,
} from '../dist/modules/dashboard/store-sales-payload.util.js';

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

const pending = {
  salesChannel: 'online',
  payable: 1200,
  onlineCod: { status: 'pending', amount: 1200 },
  payments: [],
};

assert(isOnlineCodBill(pending), 'online bill');
assert(isOnlineCodPending(pending), 'pending');
assert(parseOnlineCodAmount(pending) === 1200, 'amount');

const received = {
  salesChannel: 'online',
  onlineCod: {
    status: 'received',
    amount: 1200,
    transactionNo: 'TXN-99',
  },
  payments: [{ provider: 'Cash', amount: 1200 }],
};

assert(!isOnlineCodPending(received), 'not pending');
assert(parseOnlineCodTransactionNo(received) === 'TXN-99', 'txn');

const storeBill = { salesChannel: 'store', payable: 500 };
assert(!isOnlineCodBill(storeBill), 'store channel');

console.log('online-cod util tests: ok');
