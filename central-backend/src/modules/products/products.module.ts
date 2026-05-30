import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { BatchExpiryDetail, BatchExpiryDetailSchema } from '../batch-expiry-details/schemas/batch-expiry-detail.schema';
import { BatchSelection, BatchSelectionSchema } from '../batch-selections/schemas/batch-selection.schema';
import { Brand, BrandSchema } from '../brands/schemas/brand.schema';
import { CategoriesModule } from '../categories/categories.module';
import { Category, CategorySchema } from '../categories/schemas/category.schema';
import { Colour, ColourSchema } from '../colours/schemas/colour.schema';
import { Department, DepartmentSchema } from '../departments/schemas/department.schema';
import { DocumentNumbersModule } from '../document-numbers/document-numbers.module';
import { GstUom, GstUomSchema } from '../gst-uoms/schemas/gst-uom.schema';
import { HsnCode, HsnCodeSchema } from '../hsn-codes/schemas/hsn-code.schema';
import { IndentType, IndentTypeSchema } from '../indent-types/schemas/indent-type.schema';
import { ItemPrepStatus, ItemPrepStatusSchema } from '../item-prep-statuses/schemas/item-prep-status.schema';
import { Manufacturer, ManufacturerSchema } from '../manufacturers/schemas/manufacturer.schema';
import { OfferGroup, OfferGroupSchema } from '../offer-groups/schemas/offer-group.schema';
import { PackedConfirmation, PackedConfirmationSchema } from '../packed-confirmations/schemas/packed-confirmation.schema';
import { PoQtyPolicy, PoQtyPolicySchema } from '../po-qty-policies/schemas/po-qty-policy.schema';
import { ProductStatus, ProductStatusSchema } from '../product-statuses/schemas/product-status.schema';
import { SellByType, SellByTypeSchema } from '../sell-by-types/schemas/sell-by-type.schema';
import { SkuOrderGroup, SkuOrderGroupSchema } from '../sku-order-groups/schemas/sku-order-group.schema';
import { SkuType, SkuTypeSchema } from '../sku-types/schemas/sku-type.schema';
import { SubCategoriesModule } from '../sub-categories/sub-categories.module';
import { SubCategory, SubCategorySchema } from '../sub-categories/schemas/sub-category.schema';
import { Supplier, SupplierSchema } from '../suppliers/schemas/supplier.schema';
import { UomSub, UomSubSchema } from '../uom-subs/schemas/uom-sub.schema';
import { WeightSize, WeightSizeSchema } from '../weight-sizes/schemas/weight-size.schema';
import { WeightUnit, WeightUnitSchema } from '../weight-units/schemas/weight-unit.schema';
import { MasterLookupService } from './import/master-lookup.service';
import { ProductImportService } from './import/product-import.service';
import { ProductImportController } from './product-import.controller';
import { ProductSkuGenerator } from './product-sku.generator';
import { Product, ProductSchema } from './schemas/product.schema';
import { ProductsController } from './products.controller';
import { ProductsService } from './products.service';

@Module({
  imports: [
    DocumentNumbersModule,
    CategoriesModule,
    SubCategoriesModule,
    MongooseModule.forFeature([
      { name: Product.name, schema: ProductSchema },
      { name: Supplier.name, schema: SupplierSchema },
      { name: Department.name, schema: DepartmentSchema },
      { name: Category.name, schema: CategorySchema },
      { name: SubCategory.name, schema: SubCategorySchema },
      { name: Manufacturer.name, schema: ManufacturerSchema },
      { name: Brand.name, schema: BrandSchema },
      { name: Colour.name, schema: ColourSchema },
      { name: ProductStatus.name, schema: ProductStatusSchema },
      { name: HsnCode.name, schema: HsnCodeSchema },
      { name: GstUom.name, schema: GstUomSchema },
      { name: UomSub.name, schema: UomSubSchema },
      { name: WeightSize.name, schema: WeightSizeSchema },
      { name: WeightUnit.name, schema: WeightUnitSchema },
      { name: OfferGroup.name, schema: OfferGroupSchema },
      { name: SkuType.name, schema: SkuTypeSchema },
      { name: SkuOrderGroup.name, schema: SkuOrderGroupSchema },
      { name: IndentType.name, schema: IndentTypeSchema },
      { name: BatchExpiryDetail.name, schema: BatchExpiryDetailSchema },
      { name: ItemPrepStatus.name, schema: ItemPrepStatusSchema },
      { name: PackedConfirmation.name, schema: PackedConfirmationSchema },
      { name: PoQtyPolicy.name, schema: PoQtyPolicySchema },
      { name: SellByType.name, schema: SellByTypeSchema },
      { name: BatchSelection.name, schema: BatchSelectionSchema },
    ]),
  ],
  controllers: [ProductsController, ProductImportController],
  providers: [ProductsService, ProductSkuGenerator, MasterLookupService, ProductImportService],
  exports: [ProductsService, ProductSkuGenerator],
})
export class ProductsModule {}
