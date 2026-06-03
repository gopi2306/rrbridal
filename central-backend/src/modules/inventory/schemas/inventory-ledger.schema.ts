import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument } from 'mongoose';

export type InventoryLedgerDocument = HydratedDocument<InventoryLedgerEntry>;

export type InventoryLocationKind = 'warehouse' | 'store' | 'in_transit';

@Schema({ timestamps: true })
export class InventoryLedgerEntry {
  @ApiProperty()
  @Prop({ required: true, index: true })
  sku!: string;

  @ApiProperty()
  @Prop({ required: true })
  qtyDelta!: number;

  @ApiProperty({ enum: ['warehouse', 'store', 'in_transit'], required: false, description: 'Legacy rows omit this and are treated as warehouse' })
  @Prop({ type: String, enum: ['warehouse', 'store', 'in_transit'], index: true })
  locationKind?: InventoryLocationKind;

  @ApiProperty({ required: false, description: 'Required when locationKind is store' })
  @Prop({ index: true })
  storeId?: string;

  @ApiProperty({
    required: false,
    description: 'Warehouse Location.code when locationKind is warehouse or in_transit; legacy rows omit this',
  })
  @Prop({ trim: true, lowercase: true, index: true })
  locationCode?: string;

  @ApiProperty()
  @Prop({ required: true, index: true })
  sourceType!: string; // e.g. GoodsReceiptPosted

  @ApiProperty()
  @Prop({ required: true, index: true })
  sourceId!: string; // receipt id

  @ApiProperty({ required: false })
  @Prop()
  note?: string;
}

export const InventoryLedgerSchema = SchemaFactory.createForClass(InventoryLedgerEntry);

