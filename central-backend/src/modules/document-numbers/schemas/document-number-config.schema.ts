import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type DocumentNumberConfigDocument = HydratedDocument<DocumentNumberConfig>;

@Schema({ collection: 'document_number_configs', timestamps: true })
export class DocumentNumberConfig {
  @Prop({ required: true, unique: true, index: true })
  configKey!: string;

  /** Literal prefix before the padded sequence; use "" for numbers only (e.g. 100001). */
  @Prop({ required: true, default: '' })
  prefix!: string;

  @Prop({ required: true, default: 6 })
  padLength!: number;

  @Prop({ required: true, default: 1 })
  startFrom!: number;

  @Prop()
  label?: string;

  @Prop()
  description?: string;
}

export const DocumentNumberConfigSchema = SchemaFactory.createForClass(DocumentNumberConfig);
