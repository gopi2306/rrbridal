import { BreadcrumbBar } from '@/components/layout/BreadcrumbBar'
import { Button } from '@/components/ui/Button'
import { useShop } from '@/context/ShopContext'

export function LoginPage() {
  const { profile } = useShop()

  return (
    <>
      <BreadcrumbBar items={[{ label: 'Home', href: '/' }, { label: 'Retailer account' }]} />

      <section className="mx-auto max-w-md page-x py-16 text-center">
        <h1 className="font-display text-3xl font-semibold text-ink sm:text-4xl">Retailer account</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Online authentication is not enabled. Your retailer profile is stored locally and used only for enquiries.
        </p>
        {profile && (
          <div className="mt-6 rounded-2xl border border-ink/8 bg-surface p-5 text-left text-sm">
            <p className="font-semibold text-ink">{profile.businessName}</p>
            <p className="mt-1 text-ink-muted">{profile.contactName} · {profile.email} · {profile.phone}</p>
          </div>
        )}
        <Button to="/register" className="mt-8">
          {profile ? 'Edit retailer profile' : 'Create retailer profile'}
        </Button>
      </section>
    </>
  )
}
