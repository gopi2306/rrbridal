import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { BreadcrumbBar } from '@/components/layout/BreadcrumbBar'
import { Button } from '@/components/ui/Button'
import { useShop } from '@/context/ShopContext'
import type { RetailerProfile } from '@/types'

const EMPTY_PROFILE: RetailerProfile = {
  businessName: '',
  contactName: '',
  email: '',
  phone: '',
  address: '',
  city: '',
  state: '',
  pincode: '',
}

export function RegisterPage() {
  const navigate = useNavigate()
  const { profile, saveProfile } = useShop()
  const [form, setForm] = useState<RetailerProfile>(profile ?? EMPTY_PROFILE)
  const [error, setError] = useState('')

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!saveProfile(form)) {
      setError('Business, contact name, email, and phone are required.')
      return
    }
    navigate('/cart')
  }

  return (
    <>
      <BreadcrumbBar items={[{ label: 'Home', href: '/' }, { label: 'Register' }]} />

      <section className="mx-auto max-w-md page-x py-12 md:py-16">
        <h1 className="font-display text-3xl font-semibold text-ink sm:text-4xl">Retailer profile</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Used to send wholesale enquiries. Details stay in this browser and no password is stored.
        </p>

        <form onSubmit={handleSubmit} className="mt-8 space-y-5">
          <div>
            <label htmlFor="reg-name" className="block text-sm font-medium text-ink">
              Business name
            </label>
            <input
              id="reg-name"
              type="text"
              required
              value={form.businessName}
              onChange={(e) => setForm({ ...form, businessName: e.target.value })}
              className="mt-2 w-full rounded-xl border border-ink/10 bg-surface px-4 py-3 text-sm text-ink"
              placeholder="Your store name"
            />
          </div>
          <div>
            <label htmlFor="reg-contact" className="block text-sm font-medium text-ink">
              Contact name
            </label>
            <input
              id="reg-contact"
              required
              value={form.contactName}
              onChange={(e) => setForm({ ...form, contactName: e.target.value })}
              className="mt-2 w-full rounded-xl border border-ink/10 bg-surface px-4 py-3 text-sm text-ink"
              placeholder="Buyer / owner name"
            />
          </div>
          {([
            ['email', 'Email', 'email', 'you@retailer.com'],
            ['phone', 'Phone', 'tel', '+91 98765 43210'],
            ['address', 'Address', 'text', 'Street / area'],
            ['city', 'City', 'text', 'City'],
            ['state', 'State', 'text', 'State'],
            ['pincode', 'Pincode', 'text', 'Postal code'],
          ] as const).map(([key, label, type, placeholder]) => (
            <div key={key}>
              <label htmlFor={`reg-${key}`} className="block text-sm font-medium text-ink">{label}</label>
            <input
              id={`reg-${key}`}
              type={type}
              required={key === 'email' || key === 'phone'}
              value={form[key] ?? ''}
              onChange={(e) => setForm({ ...form, [key]: e.target.value })}
              className="mt-2 w-full rounded-xl border border-ink/10 bg-surface px-4 py-3 text-sm text-ink"
              placeholder={placeholder}
            />
            </div>
          ))}
          {error && <p className="text-sm text-red-600">{error}</p>}
          <Button className="w-full">Save profile</Button>
        </form>
      </section>
    </>
  )
}
