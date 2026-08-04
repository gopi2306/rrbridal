import { Link } from 'react-router-dom'
import site from '@/data/site'
import { categories } from '@/lib/data'
import { generalEnquiryUrl } from '@/lib/whatsapp'

export function Footer() {
  return (
    <footer className="border-t border-outline/20 bg-obsidian text-inverse">
      <div className="mx-auto grid max-w-container-max gap-10 px-margin-mobile py-14 md:grid-cols-3 md:px-margin-desktop">
        <div>
          <Link
            to="/"
            className="inline-flex items-center rounded-full border border-outline/60 bg-canvas/82 px-3 py-2 shadow-soft ring-1 ring-black/[0.03]"
          >
            <img src="/logo.png" alt={site.name} className="h-7 w-auto sm:h-8" />
          </Link>
          <p className="mt-4 text-sm text-inverse-muted">{site.tagline}</p>
          <p className="mt-2 text-sm text-inverse-faint">{site.locationNote}</p>
        </div>

        <div>
          <p className="label-caps text-inverse-faint">Collections</p>
          <ul className="mt-4 space-y-2">
            {categories.slice(0, 6).map((cat) => (
              <li key={cat.id}>
                <Link to={cat.href} className="text-sm text-inverse-muted transition-colors hover:text-brand">
                  {cat.title}
                </Link>
              </li>
            ))}
          </ul>
        </div>

        <div>
          <p className="label-caps text-inverse-faint">Contact</p>
          <p className="mt-4 text-sm leading-relaxed text-inverse-muted">{site.address}</p>
          <a
            href={`tel:${site.phone.replace(/\s/g, '')}`}
            className="mt-2 block text-sm text-inverse transition-colors hover:text-brand"
          >
            {site.phone}
          </a>
          <a
            href={generalEnquiryUrl()}
            target="_blank"
            rel="noopener noreferrer"
            className="label-caps mt-4 inline-flex bg-brand px-5 py-2.5 text-xs text-white hover:bg-brand-dark"
          >
            WhatsApp
          </a>
          <p className="mt-8 text-xs text-inverse-faint">{site.copyright}</p>
        </div>
      </div>
    </footer>
  )
}
