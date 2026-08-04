import { Hero } from '@/components/home/Hero'
import { CategoryCollage } from '@/components/home/CategoryCollage'
import { TrendingSplit } from '@/components/home/TrendingSplit'
import { NewArrivalsShowcase } from '@/components/home/NewArrivalsShowcase'
import { CampaignOverlay } from '@/components/home/CampaignOverlay'
import { PremiumLine } from '@/components/home/PremiumLine'
import { RetailVoices } from '@/components/home/RetailVoices'
import { TradeAssurance } from '@/components/home/TradeAssurance'
import { WhatsAppStrip } from '@/components/home/WhatsAppStrip'

export function HomePage() {
  return (
    <>
      <Hero />
      <CategoryCollage />
      <TrendingSplit />
      <NewArrivalsShowcase />
      <CampaignOverlay />
      <PremiumLine />
      <RetailVoices />
      <TradeAssurance />
      <WhatsAppStrip />
    </>
  )
}
