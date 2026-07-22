import { Injectable, Logger, OnModuleInit } from '@nestjs/common';
import { InjectConnection } from '@nestjs/mongoose';
import { Connection, Types } from 'mongoose';
import { GoodsReceiptsService } from '../goods-receipts/goods-receipts.service';
import { StockTransfersService } from '../stock-transfers/stock-transfers.service';
import {
  TEST_SEED_BATCH_EXPIRY_CODES,
  TEST_SEED_BATCH_SELECTION_CODES,
  TEST_SEED_BRANCH_CODES,
  TEST_SEED_BRAND_CODES,
  TEST_SEED_CATEGORY_CODES,
  TEST_SEED_COLOUR_CODES,
  TEST_SEED_CUSTOMER_CODES,
  TEST_SEED_DEPARTMENT_CODES,
  TEST_SEED_DIVISION_CODES,
  TEST_SEED_GST_UOM_CODES,
  TEST_SEED_HSN_CODES,
  TEST_SEED_INDENT_TYPE_CODES,
  TEST_SEED_INTENT_NOS,
  TEST_SEED_ITEM_PREP_CODES,
  TEST_SEED_LOCATION_CODES,
  TEST_SEED_MANUFACTURER_CODES,
  TEST_SEED_OFFER_GROUP_CODES,
  TEST_SEED_PACKED_CONFIRMATION_CODES,
  TEST_SEED_PO_NOS,
  TEST_SEED_PO_QTY_POLICY_CODES,
  TEST_SEED_PRODUCT_STATUS_CODES,
  TEST_SEED_PROMOTION_CODES,
  TEST_SEED_PURCHASE_RETURN_NOS,
  TEST_SEED_RECEIPT_NOS,
  TEST_SEED_SELL_BY_CODES,
  TEST_SEED_SKU_ORDER_GROUP_CODES,
  TEST_SEED_SKU_TYPE_CODES,
  TEST_SEED_SKUS,
  TEST_SEED_SUB_CATEGORY_CODES,
  TEST_SEED_SUPPLIER_NAMES,
  TEST_SEED_TRANSFER_NOS,
  TEST_SEED_UOM_SUB_CODES,
  TEST_SEED_WEIGHT_SIZE_CODES,
  TEST_SEED_WEIGHT_UNIT_CODES,
} from './test-data-seed.keys';

@Injectable()
export class TestDataSeedService implements OnModuleInit {
  private readonly logger = new Logger(TestDataSeedService.name);
  private refs: Record<string, string> = {};

  constructor(
    @InjectConnection() private readonly connection: Connection,
    private readonly goodsReceiptsService: GoodsReceiptsService,
    private readonly stockTransfersService: StockTransfersService,
  ) {}

  async onModuleInit() {
    if (process.env.SEED_TEST_DATA !== 'true') return;
    this.logger.log('SEED_TEST_DATA=true — seeding flow-wise test data …');

    await this.seedLayer1Lookups();
    await this.seedLayer2Masters();
    await this.seedLayer3Products();
    await this.seedLayer4Procurement();
    await this.seedLayer5StoreOps();
    await this.seedLayer6Returns();
    await this.seedLayer7PromotionSchemes();

    this.logger.log('Flow-wise test data seeding complete.');
  }

  // ─── Helpers ─────────────────────────────────────────────────

  /**
   * Idempotent upsert: inserts the doc only when uniqueFilter matches nothing.
   * Stores the resulting _id under refKey for cross-layer references.
   */
  private async upsert(
    modelName: string,
    uniqueFilter: Record<string, unknown>,
    doc: Record<string, unknown>,
    refKey?: string,
  ) {
    const model = this.connection.model(modelName);
    const result = await model.updateOne(
      uniqueFilter,
      { $setOnInsert: { ...uniqueFilter, ...doc } },
      { upsert: true },
    );
    const saved: any = await model.findOne(uniqueFilter).lean();
    if (result.upsertedCount > 0) {
      this.logger.log(`  + ${modelName} created: ${JSON.stringify(uniqueFilter)}`);
    }
    if (refKey && saved) {
      this.refs[refKey] = String(saved._id);
    }
    return saved;
  }

  // ─── Layer 1: Independent Master Lookups (22 collections) ────

  private async seedLayer1Lookups() {
    this.logger.log('Layer 1: Seeding independent lookups …');

    // Branches
    await this.upsert('Branch', { code: 'br-001' }, {
      name: 'Chennai HQ', address: 'Anna Nagar, Chennai', phone: '044-2600-0001', isActive: true,
    }, 'br-001');
    await this.upsert('Branch', { code: 'br-002' }, {
      name: 'Coimbatore Branch', address: 'RS Puram, Coimbatore', phone: '0422-2600-001', isActive: true,
    }, 'br-002');

    // Divisions
    await this.upsert('Division', { code: 'div-001' }, {
      name: 'Retail', description: 'Retail sales division', isActive: true,
    }, 'div-001');
    await this.upsert('Division', { code: 'div-002' }, {
      name: 'Wholesale', description: 'Wholesale / bulk division', isActive: true,
    }, 'div-002');

    // Locations
    await this.upsert('Location', { code: 'loc-001' }, {
      name: 'Warehouse A', address: 'Anna Nagar, Chennai', type: 'warehouse', isActive: true,
    }, 'loc-001');
    await this.upsert('Location', { code: 'loc-002' }, {
      name: 'Warehouse B', address: 'Ambattur, Chennai', type: 'warehouse', isActive: true,
    }, 'loc-002');

    // Departments
    await this.upsert('Department', { code: 'dept-001' }, { name: 'Bridal Wear', isActive: true }, 'dept-001');
    await this.upsert('Department', { code: 'dept-002' }, { name: 'Accessories', isActive: true }, 'dept-002');

    // Brands
    await this.upsert('Brand', { code: 'brand-001' }, { name: 'Sabyasachi', isActive: true }, 'brand-001');
    await this.upsert('Brand', { code: 'brand-002' }, { name: 'Manish Malhotra', isActive: true }, 'brand-002');

    // Colours
    await this.upsert('Colour', { code: 'clr-001' }, { name: 'Red', hexCode: '#DC143C', isActive: true }, 'clr-001');
    await this.upsert('Colour', { code: 'clr-002' }, { name: 'Gold', hexCode: '#FFD700', isActive: true }, 'clr-002');
    await this.upsert('Colour', { code: 'clr-003' }, { name: 'Maroon', hexCode: '#800000', isActive: true }, 'clr-003');

    // HSN Codes
    await this.upsert('HsnCode', { code: 'hsn-001' }, {
      name: 'Women Clothing', hsnCode: '6204',
      description: 'Women or girls suits, dresses, skirts', gstPercent: 12, isActive: true,
    }, 'hsn-001');
    await this.upsert('HsnCode', { code: 'hsn-002' }, {
      name: 'Imitation Jewellery', hsnCode: '7117',
      description: 'Imitation jewellery', gstPercent: 3, isActive: true,
    }, 'hsn-002');

    // GST UOMs
    await this.upsert('GstUom', { code: 'guom-001' }, { name: 'Pieces', isActive: true }, 'guom-001');
    await this.upsert('GstUom', { code: 'guom-002' }, { name: 'Meters', isActive: true }, 'guom-002');

    // Weight Sizes
    await this.upsert('WeightSize', { code: 'ws-001' }, { name: 'Standard', unit: 'gm', value: 500, isActive: true }, 'ws-001');
    await this.upsert('WeightSize', { code: 'ws-002' }, { name: 'Heavy', unit: 'gm', value: 2000, isActive: true }, 'ws-002');

    // Weight Units
    await this.upsert('WeightUnit', { code: 'wu-001' }, { name: 'Grams', baseUnit: 'gm', conversionFactor: 1, isActive: true }, 'wu-001');

    // Offer Groups
    await this.upsert('OfferGroup', { code: 'og-001' }, {
      name: 'Wedding Season 2026', description: 'Wedding season special offers',
      discountPercent: 10, validFrom: '2026-01-01', validTo: '2026-12-31', isActive: true,
    }, 'og-001');

    // Product Statuses
    await this.upsert('ProductStatus', { code: 'ps-001' }, { name: 'Active', isActive: true }, 'ps-001');
    await this.upsert('ProductStatus', { code: 'ps-002' }, { name: 'Discontinued', isActive: true }, 'ps-002');

    // UOM Subs
    await this.upsert('UomSub', { code: 'usub-001' }, { name: 'Set', baseUom: 'PCS', conversionFactor: 1, isActive: true }, 'usub-001');

    // Batch Expiry Details
    await this.upsert('BatchExpiryDetail', { code: 'bed-001' }, { name: 'No Expiry', isActive: true }, 'bed-001');

    // Item Prep Statuses
    await this.upsert('ItemPrepStatus', { code: 'ips-001' }, { name: 'Ready', isActive: true }, 'ips-001');

    // Packed Confirmations
    await this.upsert('PackedConfirmation', { code: 'pc-001' }, { name: 'Confirmed', isActive: true }, 'pc-001');

    // PO Qty Policies
    await this.upsert('PoQtyPolicy', { code: 'pqp-001' }, {
      name: 'Standard Policy', description: 'Default ordering policy',
      minQty: 1, maxQty: 100, isActive: true,
    }, 'pqp-001');

    // Sell By Types
    await this.upsert('SellByType', { code: 'sbt-001' }, { name: 'Retail', isActive: true }, 'sbt-001');

    // Batch Selections
    await this.upsert('BatchSelection', { code: 'bs-001' }, { name: 'Default Batch', isActive: true }, 'bs-001');

    // SKU Types
    await this.upsert('SkuType', { code: 'skt-001' }, { name: 'Standard SKU', isActive: true }, 'skt-001');

    // SKU Order Groups
    await this.upsert('SkuOrderGroup', { code: 'sog-001' }, { name: 'Group A', sortOrder: 1, isActive: true }, 'sog-001');

    // Indent Types
    await this.upsert('IndentType', { code: 'it-001' }, { name: 'Standard Indent', isActive: true }, 'it-001');
  }

  // ─── Layer 2: Dependent Masters ──────────────────────────────

  private async seedLayer2Masters() {
    this.logger.log('Layer 2: Seeding dependent masters …');

    // Categories
    await this.upsert('Category', { code: 'cat-001' }, {
      name: 'Lehengas', isActive: true,
    }, 'cat-001');
    await this.upsert('Category', { code: 'cat-002' }, {
      name: 'Jewellery', isActive: true,
    }, 'cat-002');

    // Sub-Categories (reference categoryId)
    await this.upsert('SubCategory', { code: 'subcat-001' }, {
      name: 'Bridal Lehenga', categoryId: this.refs['cat-001'], isActive: true,
    }, 'subcat-001');
    await this.upsert('SubCategory', { code: 'subcat-002' }, {
      name: 'Kundan Set', categoryId: this.refs['cat-002'], isActive: true,
    }, 'subcat-002');

    // Manufacturers
    await this.upsert('Manufacturer', { code: 'mfr-001' }, {
      name: 'Royal Textiles', contactPerson: 'Rajesh Mehta',
      phone: '9876543210', address: 'Surat, Gujarat', isActive: true,
    }, 'mfr-001');
    await this.upsert('Manufacturer', { code: 'mfr-002' }, {
      name: 'Jewel Craft India', contactPerson: 'Sunil Sharma',
      phone: '9876543211', address: 'Jaipur, Rajasthan', isActive: true,
    }, 'mfr-002');

    // Suppliers (no code field — upsert by name)
    await this.upsert('Supplier', { name: 'Sharma Textiles' }, {
      mobileNo: '9876500001', gstNumber: '27AABCS1234A1Z5',
      contactPerson: 'Vikram Sharma',
      city: 'Mumbai', state: 'Maharashtra', country: 'India',
      isSupplier: true, isActive: true,
    }, 'supplier-001');
    await this.upsert('Supplier', { name: 'Rajasthan Jewellers' }, {
      mobileNo: '9876500002', gstNumber: '08AABCR5678B2Z9',
      contactPerson: 'Ramesh Soni',
      city: 'Jaipur', state: 'Rajasthan', country: 'India',
      isSupplier: true, isActive: true,
    }, 'supplier-002');

    // Customers (upsert by customerCode)
    await this.upsert('Customer', { customerCode: 'CUST-001' }, {
      name: 'Walk-in Customer', phone: '0000000000', isActive: true,
    }, 'customer-001');
    await this.upsert('Customer', { customerCode: 'CUST-002' }, {
      name: 'Priya Mehta (VIP)', phone: '9988776655', email: 'priya@example.com',
      city: 'Chennai', state: 'Tamil Nadu', isActive: true,
    }, 'customer-002');
  }

  // ─── Layer 3: Products (5 SKUs) ──────────────────────────────

  private async seedLayer3Products() {
    this.logger.log('Layer 3: Seeding products …');

    const lehengaBase = {
      departmentId: this.refs['dept-001'],
      categoryId: this.refs['cat-001'],
      subCategoryId: this.refs['subcat-001'],
      hsnCodeId: this.refs['hsn-001'],
      gstPercent: 12,
      gstUomId: this.refs['guom-001'],
      productStatusId: this.refs['ps-001'],
      uomSubId: this.refs['usub-001'],
      batchExpiryDetailId: this.refs['bed-001'],
      itemPrepStatusId: this.refs['ips-001'],
      packedConfirmationId: this.refs['pc-001'],
      poQtyPolicyId: this.refs['pqp-001'],
      sellById: this.refs['sbt-001'],
      batchSelectionId: this.refs['bs-001'],
      skuTypeId: this.refs['skt-001'],
      skuOrderGroupId: this.refs['sog-001'],
      indentTypeId: this.refs['it-001'],
      offerGroupId: this.refs['og-001'],
      supplierNameId: this.refs['supplier-001'],
      manufacturerNameId: this.refs['mfr-001'],
      unit: 'PCS',
      isWeighable: false,
      itemDiscountAllowed: true,
      isActive: true,
    };

    const jewelleryBase = {
      departmentId: this.refs['dept-002'],
      categoryId: this.refs['cat-002'],
      subCategoryId: this.refs['subcat-002'],
      hsnCodeId: this.refs['hsn-002'],
      gstPercent: 3,
      gstUomId: this.refs['guom-001'],
      productStatusId: this.refs['ps-001'],
      uomSubId: this.refs['usub-001'],
      batchExpiryDetailId: this.refs['bed-001'],
      itemPrepStatusId: this.refs['ips-001'],
      packedConfirmationId: this.refs['pc-001'],
      poQtyPolicyId: this.refs['pqp-001'],
      sellById: this.refs['sbt-001'],
      batchSelectionId: this.refs['bs-001'],
      skuTypeId: this.refs['skt-001'],
      skuOrderGroupId: this.refs['sog-001'],
      indentTypeId: this.refs['it-001'],
      supplierNameId: this.refs['supplier-002'],
      manufacturerNameId: this.refs['mfr-002'],
      unit: 'PCS',
      isWeighable: false,
      itemDiscountAllowed: true,
      isActive: true,
    };

    await this.upsert('Product', { sku: 'SKU-001' }, {
      ...lehengaBase,
      itemName: 'Bridal Red Lehenga', shortName: 'Red Lehenga',
      brandId: this.refs['brand-001'], colourIds: [this.refs['clr-001']],
      weightAndSizeId: this.refs['ws-002'],
      costPrice: 25000, mrp: 45000, sellingPrice: 42000, storePrice: 40000,
      minStock: 2, reorderLevel: 5,
    });

    await this.upsert('Product', { sku: 'SKU-002' }, {
      ...lehengaBase,
      itemName: 'Gold Bridal Lehenga', shortName: 'Gold Lehenga',
      brandId: this.refs['brand-002'], colourIds: [this.refs['clr-002']],
      weightAndSizeId: this.refs['ws-002'],
      costPrice: 30000, mrp: 55000, sellingPrice: 50000, storePrice: 48000,
      minStock: 2, reorderLevel: 4,
    });

    await this.upsert('Product', { sku: 'SKU-003' }, {
      ...lehengaBase,
      itemName: 'Maroon Reception Lehenga', shortName: 'Maroon Lehenga',
      colourIds: [this.refs['clr-003']], weightAndSizeId: this.refs['ws-001'],
      costPrice: 18000, mrp: 32000, sellingPrice: 30000, storePrice: 28000,
      minStock: 3, reorderLevel: 6,
    });

    await this.upsert('Product', { sku: 'SKU-004' }, {
      ...jewelleryBase,
      itemName: 'Kundan Bridal Necklace Set', shortName: 'Kundan Necklace',
      colourIds: [this.refs['clr-002']], weightAndSizeId: this.refs['ws-001'],
      costPrice: 8000, mrp: 15000, sellingPrice: 14000, storePrice: 13000,
      minStock: 5, reorderLevel: 10,
    });

    await this.upsert('Product', { sku: 'SKU-005' }, {
      ...jewelleryBase,
      itemName: 'Kundan Maang Tikka', shortName: 'Maang Tikka',
      colourIds: [this.refs['clr-002']], weightAndSizeId: this.refs['ws-001'],
      costPrice: 3000, mrp: 6000, sellingPrice: 5500, storePrice: 5000,
      minStock: 8, reorderLevel: 15,
    });
  }

  // ─── Layer 4: Procurement (POs + GRs → warehouse inventory) ──

  private async seedLayer4Procurement() {
    this.logger.log('Layer 4: Seeding purchase orders & goods receipts …');

    const poModel = this.connection.model('PurchaseOrder');
    const grModel = this.connection.model('GoodsReceipt');

    // ── PO-1001: Lehenga supplier ─────────────────────────────
    let po1: any = await poModel.findOne({ poNo: 'PO-1001' }).lean();
    if (!po1) {
      po1 = await poModel.create({
        poNo: 'PO-1001',
        branchId: this.refs['br-001'],
        mainDivisionId: this.refs['div-001'],
        mainLocationId: this.refs['loc-001'],
        supplier: {
          supplierId: this.refs['supplier-001'],
          name: 'Sharma Textiles',
          mobile: '9876500001',
          cashDiscount: 2,
        },
        poDate: '2026-04-01',
        deliveryDate: '2026-04-15',
        status: 'received',
        lines: [
          {
            sku: 'SKU-001',
            description: 'Bridal Red Lehenga',
            recdQty: 10,
            cost: 25000,
            selling: 42000,
            mrp: 45000,
            taxPercent: 12,
            cgstPercent: 6,
            sgstPercent: 6,
          },
          {
            sku: 'SKU-002',
            description: 'Gold Bridal Lehenga',
            recdQty: 8,
            cost: 30000,
            selling: 50000,
            mrp: 55000,
            taxPercent: 12,
            cgstPercent: 6,
            sgstPercent: 6,
          },
          {
            sku: 'SKU-003',
            description: 'Maroon Reception Lehenga',
            recdQty: 6,
            cost: 18000,
            selling: 30000,
            mrp: 32000,
            taxPercent: 12,
            cgstPercent: 6,
            sgstPercent: 6,
          },
        ],
      });
      this.logger.log('  + PO-1001 created (Sharma Textiles — 3 lehenga SKUs)');
    }

    // ── PO-1002: Jewellery supplier ───────────────────────────
    let po2: any = await poModel.findOne({ poNo: 'PO-1002' }).lean();
    if (!po2) {
      po2 = await poModel.create({
        poNo: 'PO-1002',
        branchId: this.refs['br-001'],
        mainDivisionId: this.refs['div-001'],
        mainLocationId: this.refs['loc-001'],
        supplier: {
          supplierId: this.refs['supplier-002'],
          name: 'Rajasthan Jewellers',
          mobile: '9876500002',
          cashDiscount: 3,
        },
        poDate: '2026-04-05',
        deliveryDate: '2026-04-20',
        status: 'received',
        lines: [
          {
            sku: 'SKU-004',
            description: 'Kundan Bridal Necklace Set',
            recdQty: 15,
            cost: 8000,
            selling: 14000,
            mrp: 15000,
            taxPercent: 3,
            cgstPercent: 1.5,
            sgstPercent: 1.5,
          },
          {
            sku: 'SKU-005',
            description: 'Kundan Maang Tikka',
            recdQty: 20,
            cost: 3000,
            selling: 5500,
            mrp: 6000,
            taxPercent: 3,
            cgstPercent: 1.5,
            sgstPercent: 1.5,
          },
        ],
      });
      this.logger.log('  + PO-1002 created (Rajasthan Jewellers — 2 jewellery SKUs)');
    }

    // ── GR for PO-1001 ────────────────────────────────────────
    let gr1: any = await grModel.findOne({ receiptNo: 'RCV-001' }).lean();
    if (!gr1) {
      gr1 = await grModel.create({
        receiptNo: 'RCV-001',
        poId: String(po1._id),
        poNo: 'PO-1001',
        supplier: { supplierId: this.refs['supplier-001'], name: 'Sharma Textiles' },
        invoiceNo: 'INV-ST-2026-001',
        invoiceDate: '15/04/2026',
        status: 'draft',
        lines: [
          { sku: 'SKU-001', description: 'Bridal Red Lehenga', orderedQty: 10, receivedQty: 10, outcome: 'valid' },
          { sku: 'SKU-002', description: 'Gold Bridal Lehenga', orderedQty: 8, receivedQty: 8, outcome: 'valid' },
          { sku: 'SKU-003', description: 'Maroon Reception Lehenga', orderedQty: 6, receivedQty: 6, outcome: 'valid' },
        ],
      });
      this.logger.log('  + RCV-001 created (draft)');
      await this.goodsReceiptsService.postToInventory(String(gr1._id));
      this.logger.log('  + RCV-001 posted -> warehouse inventory +10/+8/+6');
    }

    // ── GR for PO-1002 ────────────────────────────────────────
    let gr2: any = await grModel.findOne({ receiptNo: 'RCV-002' }).lean();
    if (!gr2) {
      gr2 = await grModel.create({
        receiptNo: 'RCV-002',
        poId: String(po2._id),
        poNo: 'PO-1002',
        supplier: { supplierId: this.refs['supplier-002'], name: 'Rajasthan Jewellers' },
        invoiceNo: 'INV-RJ-2026-001',
        invoiceDate: '20/04/2026',
        status: 'draft',
        lines: [
          { sku: 'SKU-004', description: 'Kundan Bridal Necklace Set', orderedQty: 15, receivedQty: 15, outcome: 'valid' },
          { sku: 'SKU-005', description: 'Kundan Maang Tikka', orderedQty: 20, receivedQty: 20, outcome: 'valid' },
        ],
      });
      this.logger.log('  + RCV-002 created (draft)');
      await this.goodsReceiptsService.postToInventory(String(gr2._id));
      this.logger.log('  + RCV-002 posted -> warehouse inventory +15/+20');
    }
  }

  // ─── Layer 5: Store Operations (PIs + STs → store inventory) ─

  private async seedLayer5StoreOps() {
    this.logger.log('Layer 5: Seeding purchase intents & stock transfers …');

    const piModel = this.connection.model('PurchaseIntent');
    const stModel = this.connection.model('StockTransfer');
    const warehouseAId = this.refs['loc-001'];
    const warehouseBId = this.refs['loc-002'];

    // ── Purchase Intent for store-001 ─────────────────────────
    let pi1: any = await piModel.findOne({ intentNo: 'PINV-001' }).lean();
    if (!pi1) {
      pi1 = await piModel.create({
        intentNo: 'PINV-001',
        storeId: 'store-001',
        status: 'approved',
        remarks: 'Monthly stock replenishment for Main branch',
        lines: [
          {
            sku: 'SKU-001',
            description: 'Bridal Red Lehenga',
            requestedQty: 2,
            stockClassification: 'Normal Stock',
            toKind: 'warehouse',
            toLocationId: new Types.ObjectId(warehouseAId),
            remarks: 'Prefer from Warehouse A',
          },
          {
            sku: 'SKU-004',
            description: 'Kundan Bridal Necklace Set',
            requestedQty: 3,
            stockClassification: 'Reserve / Seasonal',
            toKind: 'warehouse',
            toLocationId: new Types.ObjectId(warehouseBId),
          },
        ],
      });
      this.logger.log('  + PINV-001 created (store-001: SKU-001 x2, SKU-004 x3)');
    }

    // ── Purchase Intent for store-002 ─────────────────────────
    let pi2: any = await piModel.findOne({ intentNo: 'PINV-002' }).lean();
    if (!pi2) {
      pi2 = await piModel.create({
        intentNo: 'PINV-002',
        storeId: 'store-002',
        status: 'approved',
        remarks: 'Monthly stock replenishment for T Nagar branch',
        lines: [
          {
            sku: 'SKU-002',
            description: 'Gold Bridal Lehenga',
            requestedQty: 1,
            stockClassification: 'Normal Stock',
            toKind: 'warehouse',
            toLocationId: new Types.ObjectId(warehouseAId),
          },
          { sku: 'SKU-005', description: 'Kundan Maang Tikka', requestedQty: 2, remarks: 'Rush display stock' },
        ],
      });
      this.logger.log('  + PINV-002 created (store-002: SKU-002 x1, SKU-005 x2)');
    }

    // ── Stock Transfer 1: Warehouse A → store-001 (completed path) ─
    let st1: any = await stModel.findOne({ transferNo: 'TR-001' }).lean();
    if (!st1) {
      st1 = await stModel.create({
        transferNo: 'TR-001',
        direction: 'warehouse_to_store',
        fromKind: 'warehouse',
        fromLocationId: new Types.ObjectId(warehouseAId),
        toStoreId: 'store-001',
        purchaseIntentId: new Types.ObjectId(String(pi1._id)),
        status: 'draft',
        transferDate: '2026-04-25',
        remarks: 'Transfer against PINV-001 (seed: Warehouse A)',
        stockClassification: 'Normal Stock',
        lines: [
          { sku: 'SKU-001', description: 'Bridal Red Lehenga', qty: 2 },
          { sku: 'SKU-004', description: 'Kundan Bridal Necklace Set', qty: 3 },
        ],
      });
      this.logger.log('  + TR-001 created (draft, fromLocationId=Warehouse A)');

      const st1Id = String(st1._id);
      await this.stockTransfersService.setStatus(st1Id, 'in_transit');
      this.logger.log('  + TR-001 -> in_transit (warehouse -qty, in_transit +qty)');
      await this.stockTransfersService.receiveAtStore(st1Id, {
        storeId: 'store-001',
        receivedBy: 'seed',
        lines: [
          { sku: 'SKU-001', qty: 2 },
          { sku: 'SKU-004', qty: 3 },
        ],
      });
      this.logger.log('  + TR-001 -> awaiting_intake (store receive)');
      await this.stockTransfersService.setStatus(st1Id, 'completed');
      this.logger.log('  + TR-001 -> completed (in_transit -qty, store-001 +qty)');

      await piModel.updateOne({ _id: pi1._id }, { $set: { status: 'fulfilled' } });
      this.logger.log('  + PINV-001 -> fulfilled');
    }

    // ── Stock Transfer 2: Warehouse B → store-002 (completed path) ─
    let st2: any = await stModel.findOne({ transferNo: 'TR-002' }).lean();
    if (!st2) {
      st2 = await stModel.create({
        transferNo: 'TR-002',
        direction: 'warehouse_to_store',
        fromKind: 'warehouse',
        fromLocationId: new Types.ObjectId(warehouseBId),
        toStoreId: 'store-002',
        purchaseIntentId: new Types.ObjectId(String(pi2._id)),
        status: 'draft',
        transferDate: '2026-04-26',
        remarks: 'Transfer against PINV-002 (seed: Warehouse B)',
        stockClassification: 'Reserve / Seasonal',
        lines: [
          { sku: 'SKU-002', description: 'Gold Bridal Lehenga', qty: 1 },
          { sku: 'SKU-005', description: 'Kundan Maang Tikka', qty: 2 },
        ],
      });
      this.logger.log('  + TR-002 created (draft, fromLocationId=Warehouse B)');

      const st2Id = String(st2._id);
      await this.stockTransfersService.setStatus(st2Id, 'in_transit');
      this.logger.log('  + TR-002 -> in_transit (warehouse -qty, in_transit +qty)');
      await this.stockTransfersService.receiveAtStore(st2Id, {
        storeId: 'store-002',
        receivedBy: 'seed',
        lines: [
          { sku: 'SKU-002', qty: 1 },
          { sku: 'SKU-005', qty: 2 },
        ],
      });
      this.logger.log('  + TR-002 -> awaiting_intake (store receive)');
      await this.stockTransfersService.setStatus(st2Id, 'completed');
      this.logger.log('  + TR-002 -> completed (in_transit -qty, store-002 +qty)');

      await piModel.updateOne({ _id: pi2._id }, { $set: { status: 'fulfilled' } });
      this.logger.log('  + PINV-002 -> fulfilled');
    }

    // ── Stock Transfer 3: draft, no PI — for list/filter & locationId API tests ─
    let st3: any = await stModel.findOne({ transferNo: 'TR-003' }).lean();
    if (!st3) {
      await stModel.create({
        transferNo: 'TR-003',
        direction: 'warehouse_to_store',
        fromKind: 'warehouse',
        fromLocationId: new Types.ObjectId(warehouseAId),
        toStoreId: 'store-001',
        status: 'draft',
        transferDate: '2026-05-10',
        remarks: 'Seed draft (no purchase intent) — safe to edit or ship in tests',
        stockClassification: 'Normal Stock',
        lines: [{ sku: 'SKU-003', description: 'Maroon Reception Lehenga', qty: 1 }],
      });
      this.logger.log('  + TR-003 created (draft, no PI, Warehouse A)');
    }
  }

  // ─── Layer 6: Purchase Returns ───────────────────────────────

  private async seedLayer6Returns() {
    this.logger.log('Layer 6: Seeding purchase returns …');

    const prModel = this.connection.model('PurchaseReturn');

    const existing = await prModel.findOne({ purchaseReturnNo: 'PR-001' }).lean();
    if (!existing) {
      await prModel.create({
        purchaseReturnNo: 'PR-001',
        branchId: this.refs['br-001'],
        mainDivisionId: this.refs['div-001'],
        mainLocationId: this.refs['loc-001'],
        supplier: {
          supplierId: this.refs['supplier-001'],
          name: 'Sharma Textiles',
          mobile: '9876500001',
          cashDiscPercent: 2,
        },
        purchaseReturnDate: '2026-05-01',
        pucOutSlipNo: 'OUT-001',
        lines: [
          { sku: 'SKU-003', description: 'Maroon Reception Lehenga (damaged)', qty: 1, cost: 18000, mrp: 32000, taxPercent: 12 },
        ],
      });
      this.logger.log('  + PR-001 created (SKU-003 x1 returned to Sharma Textiles)');
    }
  }

  // ─── Layer 7: Promotion schemes & offers (SKU-001 test promos) ─

  private async seedLayer7PromotionSchemes() {
    this.logger.log('Layer 7: Seeding promotion schemes & offers …');

    const validFrom = new Date('2026-01-01T00:00:00.000Z');
    const validTo = new Date('2026-12-31T23:59:59.999Z');

    // Scheme: Buy 2 Get 1 free on SKU-001 (Bridal Red Lehenga) — scan qty 3 to see discount.
    await this.upsert('PromotionScheme', { code: 'sku001-bxgy' }, {
      name: 'SKU-001 Buy 2 Get 1',
      description: 'Seed scheme — Buy 2 Get 1 free on Bridal Red Lehenga (cheapest unit free)',
      kind: 'scheme',
      type: 'item',
      priority: 10,
      isActive: true,
      stacking: 'best_benefit',
      storeIds: [],
      validFrom,
      validTo,
      timeWindows: [],
      conditions: {
        skus: ['SKU-001'],
        categoryIds: [],
        brandIds: [],
        offerGroupIds: [],
        customerTypes: [],
        customerCodes: [],
        requiredSkus: [],
        minLineQty: 3,
      },
      benefit: {
        mode: 'buy_x_get_y',
        buyQty: 2,
        getQty: 1,
        freeOn: 'cheapest',
        slabs: [],
        comboSkus: [],
      },
    });

    // Offer: 5% off SKU-001 — applies on single-qty bills (best_benefit vs BXGY when qty ≥ 3).
    await this.upsert('PromotionScheme', { code: 'sku001-offer-5pct' }, {
      name: 'SKU-001 Wedding Offer 5%',
      description: 'Seed marketing offer — 5% off Bridal Red Lehenga',
      kind: 'offer',
      type: 'item',
      priority: 20,
      isActive: true,
      stacking: 'best_benefit',
      storeIds: [],
      validFrom,
      validTo,
      timeWindows: [],
      conditions: {
        skus: ['SKU-001'],
        categoryIds: [],
        brandIds: [],
        offerGroupIds: [],
        customerTypes: [],
        customerCodes: [],
        requiredSkus: [],
      },
      benefit: {
        mode: 'percent_off',
        discountPercent: 5,
        slabs: [],
        comboSkus: [],
      },
    });

    this.logger.log('  + sku001-bxgy (scheme: Buy 2 Get 1 on SKU-001)');
    this.logger.log('  + sku001-offer-5pct (offer: 5% off SKU-001)');
  }

  /** Removes documents created by flow-wise test seed (SEED_TEST_DATA). Does not touch users/stores/company profile. */
  async clearTestData(): Promise<void> {
    this.logger.log('Clearing flow-wise test seed data …');

    const del = async (modelName: string, filter: Record<string, unknown>) => {
      const result = await this.connection.model(modelName).deleteMany(filter);
      if (result.deletedCount > 0) {
        this.logger.log(`  - ${modelName}: deleted ${result.deletedCount}`);
      }
    };

    await del('PromotionScheme', { code: { $in: [...TEST_SEED_PROMOTION_CODES] } });
    await del('PurchaseReturn', { purchaseReturnNo: { $in: [...TEST_SEED_PURCHASE_RETURN_NOS] } });
    await del('StockTransfer', { transferNo: { $in: [...TEST_SEED_TRANSFER_NOS] } });
    await del('PurchaseIntent', { intentNo: { $in: [...TEST_SEED_INTENT_NOS] } });
    await del('GoodsReceipt', { receiptNo: { $in: [...TEST_SEED_RECEIPT_NOS] } });
    await del('PurchaseOrder', { poNo: { $in: [...TEST_SEED_PO_NOS] } });

    const db = this.connection.db;
    if (!db) throw new Error('MongoDB connection is not ready');
    const ledger = await db
      .collection('inventoryledgerentries')
      .deleteMany({ sku: { $in: [...TEST_SEED_SKUS] } });
    if (ledger.deletedCount > 0) {
      this.logger.log(`  - inventoryledgerentries: deleted ${ledger.deletedCount}`);
    }

    await del('Product', { sku: { $in: [...TEST_SEED_SKUS] } });
    await del('Customer', { customerCode: { $in: [...TEST_SEED_CUSTOMER_CODES] } });
    await del('Supplier', { name: { $in: [...TEST_SEED_SUPPLIER_NAMES] } });
    await del('Manufacturer', { code: { $in: [...TEST_SEED_MANUFACTURER_CODES] } });
    await del('SubCategory', { code: { $in: [...TEST_SEED_SUB_CATEGORY_CODES] } });
    await del('Category', { code: { $in: [...TEST_SEED_CATEGORY_CODES] } });
    await del('Branch', { code: { $in: [...TEST_SEED_BRANCH_CODES] } });
    await del('Division', { code: { $in: [...TEST_SEED_DIVISION_CODES] } });
    await del('Location', { code: { $in: [...TEST_SEED_LOCATION_CODES] } });
    await del('Department', { code: { $in: [...TEST_SEED_DEPARTMENT_CODES] } });
    await del('Brand', { code: { $in: [...TEST_SEED_BRAND_CODES] } });
    await del('Colour', { code: { $in: [...TEST_SEED_COLOUR_CODES] } });
    await del('HsnCode', { code: { $in: [...TEST_SEED_HSN_CODES] } });
    await del('GstUom', { code: { $in: [...TEST_SEED_GST_UOM_CODES] } });
    await del('WeightSize', { code: { $in: [...TEST_SEED_WEIGHT_SIZE_CODES] } });
    await del('WeightUnit', { code: { $in: [...TEST_SEED_WEIGHT_UNIT_CODES] } });
    await del('OfferGroup', { code: { $in: [...TEST_SEED_OFFER_GROUP_CODES] } });
    await del('ProductStatus', { code: { $in: [...TEST_SEED_PRODUCT_STATUS_CODES] } });
    await del('UomSub', { code: { $in: [...TEST_SEED_UOM_SUB_CODES] } });
    await del('BatchExpiryDetail', { code: { $in: [...TEST_SEED_BATCH_EXPIRY_CODES] } });
    await del('ItemPrepStatus', { code: { $in: [...TEST_SEED_ITEM_PREP_CODES] } });
    await del('PackedConfirmation', { code: { $in: [...TEST_SEED_PACKED_CONFIRMATION_CODES] } });
    await del('PoQtyPolicy', { code: { $in: [...TEST_SEED_PO_QTY_POLICY_CODES] } });
    await del('SellByType', { code: { $in: [...TEST_SEED_SELL_BY_CODES] } });
    await del('BatchSelection', { code: { $in: [...TEST_SEED_BATCH_SELECTION_CODES] } });
    await del('SkuType', { code: { $in: [...TEST_SEED_SKU_TYPE_CODES] } });
    await del('SkuOrderGroup', { code: { $in: [...TEST_SEED_SKU_ORDER_GROUP_CODES] } });
    await del('IndentType', { code: { $in: [...TEST_SEED_INDENT_TYPE_CODES] } });

    this.logger.log('Test seed data cleared.');
  }
}
