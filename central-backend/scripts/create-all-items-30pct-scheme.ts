/**
 * Upsert promotion scheme: 30% off all items, valid today through tomorrow (UTC).
 * Usage: npx ts-node scripts/create-all-items-30pct-scheme.ts
 */
import mongoose from 'mongoose';

const CODE = 'all-items-flat-30';

function startOfTodayUtc(): Date {
  const d = new Date();
  d.setUTCHours(0, 0, 0, 0);
  return d;
}

function endOfTomorrowUtc(): Date {
  const d = startOfTodayUtc();
  d.setUTCDate(d.getUTCDate() + 2);
  d.setUTCMilliseconds(-1);
  return d;
}

async function main() {
  const uri = process.env.MONGO_URI ?? 'mongodb://localhost:27017/rr_bridal_central';
  await mongoose.connect(uri);

  const validFrom = startOfTodayUtc();
  const validTo = endOfTomorrowUtc();

  const doc = {
    code: CODE,
    name: 'All Items Flat 30% Off',
    description: '30% off every item — valid today and tomorrow',
    kind: 'scheme',
    type: 'item',
    priority: 5,
    isActive: true,
    stacking: 'best_benefit',
    storeIds: [] as string[],
    validFrom,
    validTo,
    timeWindows: [] as unknown[],
    conditions: {
      skus: [] as string[],
      categoryIds: [] as string[],
      brandIds: [] as string[],
      offerGroupIds: [] as string[],
      customerTypes: [] as string[],
      customerCodes: [] as string[],
      requiredSkus: [] as unknown[],
    },
    benefit: {
      mode: 'percent_off',
      discountPercent: 30,
      slabs: [] as unknown[],
      comboSkus: [] as string[],
    },
  };

  const col = mongoose.connection.collection('promotion_schemes');
  const result = await col.findOneAndUpdate(
    { code: CODE },
    { $set: doc, $unset: { deletedAt: '' } },
    { upsert: true, returnDocument: 'after' },
  );

  console.log('Promotion scheme saved:');
  console.log(`  code:       ${result?.code}`);
  console.log(`  name:       ${result?.name}`);
  console.log(`  discount:   30% (all items)`);
  console.log(`  validFrom:  ${validFrom.toISOString()}`);
  console.log(`  validTo:    ${validTo.toISOString()}`);
  console.log(`  id:         ${result?._id}`);
  console.log('Run store sync to pull this scheme to billing.');

  await mongoose.disconnect();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
