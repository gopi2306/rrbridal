import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { PoQtyPolicy, PoQtyPolicySchema } from './schemas/po-qty-policy.schema';
import { PoQtyPoliciesController } from './po-qty-policies.controller';
import { PoQtyPoliciesService } from './po-qty-policies.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: PoQtyPolicy.name, schema: PoQtyPolicySchema }])],
  controllers: [PoQtyPoliciesController],
  providers: [PoQtyPoliciesService],
  exports: [PoQtyPoliciesService],
})
export class PoQtyPoliciesModule {}
