import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type IdSequenceDocument = HydratedDocument<IdSequence>;

/** Atomic counters for human-readable document numbers. */
@Schema({ collection: 'id_sequences' })
export class IdSequence {
  @Prop({ required: true, unique: true, index: true })
  sequenceKey!: string;

  @Prop({ required: true, default: 0 })
  seq!: number;
}

export const IdSequenceSchema = SchemaFactory.createForClass(IdSequence);
