import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type IdSequenceDocument = HydratedDocument<IdSequence>;

/** Atomic counters for human-readable document numbers (e.g. product SKU). */
@Schema({ collection: 'id_sequences' })
export class IdSequence {
  @Prop({ required: true, default: 0 })
  seq!: number;
}

export const IdSequenceSchema = SchemaFactory.createForClass(IdSequence);
