import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { ApiProperty } from '@nestjs/swagger';
import { HydratedDocument, Types } from 'mongoose';

export type StockTallyDocument = HydratedDocument<StockTally>;

export type StockTallyStatus = 'draft' | 'saved';

@Schema({ _id: false })
export class StockTallyLine {
  @ApiProperty({ required: false })
  @Prop({ type: Types.ObjectId, ref: 'Product', index: true })
  productId?: Types.ObjectId;

  @ApiProperty()
  @Prop({ required: true })
  sku!: string;

  @ApiProperty()
  @Prop({ required: true, default: 0 })
  scannedQty!: number;
}

@Schema({ timestamps: true, collection: 'stock_tallies' })
export class StockTally {
  @ApiProperty({ example: 'ST-000001' })
  @Prop({ required: true, unique: true, index: true })
  tallyNo!: string;

  @ApiProperty({ example: 'store-001' })
  @Prop({ required: true, index: true })
  storeId!: string;

  @ApiProperty({ enum: ['draft', 'saved'], default: 'draft' })
  @Prop({ required: true, default: 'draft', index: true })
  status!: StockTallyStatus;

  @ApiProperty({ type: [StockTallyLine] })
  @Prop({ type: [StockTallyLine], default: [] })
  lines!: StockTallyLine[];
}

export const StockTallySchema = SchemaFactory.createForClass(StockTally);

StockTallySchema.index({ storeId: 1, status: 1 });
