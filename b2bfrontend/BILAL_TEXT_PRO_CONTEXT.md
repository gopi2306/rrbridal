# Bilal Textiles — Project Context

> **Purpose of this document:** Provide full context for incorporating an existing **billing/admin panel UI** into this project. Use this as the single source of truth when mapping admin screens, features, and data models from the billing project to Bilal Textiles.

---

## 1. What This Project Is

**Bilal Textiles** is a **B2B wholesale e-commerce catalogue** for ethnic wear (salwar suits, kurtis, Pakistani suits, partywear, etc.). It targets **retailers and boutiques**, not end consumers.

| Aspect | Detail |
|--------|--------|
| Business model | Wholesale / bulk orders only (MOQ per product) |
| Checkout flow | **No online payment** — cart records source-split API enquiries; WhatsApp remains a follow-up |
| Locations | Surat Textile Market & Hyderabad, India |
| Currencies shown | INR (primary), USD, EUR, AED, SAR listed in site config |
| Current data | Live multi-database catalog from `b2b-backend`; static files remain for marketing/fallback imagery |
| Auth | No password login; local retailer profile is attached to enquiries |

---

## 2. Tech Stack

| Layer | Technology |
|-------|------------|
| Framework | React 19 + TypeScript |
| Build | Vite 6 |
| Routing | React Router DOM 7 |
| Styling | Tailwind CSS v4 (`@tailwindcss/vite`) |
| Animation | Framer Motion 12 |
| State | React Context (`ShopContext`) + localStorage |
| Path alias | `@/` → `src/` |

### Scripts

```bash
npm run dev          # Local dev server
npm run dev:host     # Dev server accessible on LAN
npm run build        # Typecheck + production build
npm run preview      # Preview production build
npm run compress:images  # Compress images in public/
```

### Environment Variables

| Variable | Purpose |
|----------|---------|
| `VITE_COMING_SOON` | When `true`, all routes show the coming-soon page |

- `.env.production` sets `VITE_COMING_SOON=true` (production deploy is gated)
- `.env.local` can override for local testing
- See `.env.example` for template

---

## 3. Project Structure

```
bilal_textiles/
├── public/
│   ├── logo.png
│   └── images/
│       ├── dresses/     # pro-1.webp … pro-12.webp (product photos)
│       ├── hero/        # hero-01.webp … hero-05.webp
│       └── banners/
├── src/
│   ├── App.tsx              # Routes + coming-soon gate
│   ├── main.tsx             # ShopProvider wrapper
│   ├── index.css            # Tailwind theme + design tokens
│   ├── types/index.ts       # Shared TypeScript interfaces
│   ├── data/
│   │   ├── site.ts          # Business info, contact, hero copy
│   │   ├── categories.ts    # Category tree (11 top-level)
│   │   ├── products.ts      # Product catalogue (25 items)
│   │   ├── about.ts         # About page content
│   │   └── testimonials.ts  # Customer quotes
│   ├── context/
│   │   └── ShopContext.tsx  # Cart, wishlist, demo user
│   ├── lib/
│   │   ├── data.ts          # Data accessors + formatPrice
│   │   ├── filters.ts       # Catalog filter/sort logic
│   │   ├── images.ts        # Image path resolution
│   │   ├── whatsapp.ts      # WhatsApp deep-link builders
│   │   ├── format.ts        # titleCase, etc.
│   │   └── featureFlags.ts  # VITE_COMING_SOON flag
│   ├── pages/               # Route-level screens (10 pages)
│   ├── components/
│   │   ├── layout/          # Header, Footer, Layout, SiteMenu
│   │   ├── home/            # Homepage sections
│   │   ├── catalog/         # Filters, toolbar
│   │   ├── product/         # ProductCard, Gallery, Related
│   │   └── ui/              # Button, FadeIn, Tooltip, FABs
│   └── hooks/               # useParallaxPx, usePrefersReducedMotion
├── vite.config.ts
├── package.json
└── PROJECT_CONTEXT.md         # This file
```

---

## 4. Routes & Pages (Storefront)

All storefront routes are wrapped in `<Layout>` (Header + Footer + FABs) unless coming-soon is enabled.

| Route | Page | Description |
|-------|------|-------------|
| `/` | `HomePage` | Marketing landing with hero, categories, new arrivals, testimonials |
| `/about` | `AboutPage` | Company story, highlights, values |
| `/contact` | `ContactPage` | Address, phone, WhatsApp, Google Maps |
| `/cart` | `CartPage` | Line items, qty edit, subtotal, WhatsApp checkout |
| `/wishlist` | `WishlistPage` | Saved products grid |
| `/login` | `LoginPage` | Demo sign-in |
| `/register` | `RegisterPage` | Demo registration |
| `/product/:slug` | `ProductPage` | PDP: gallery, specs, add to cart, WhatsApp enquire |
| `/:categoryId/*` | `CategoryPage` | Category listing with filters & sort |
| `*` (when coming soon) | `ComingSoonPage` | Full-screen gate with WhatsApp CTA |

### Category URL Pattern

Categories use nested paths:

```
/salwar-kameez                    → category landing
/salwar-kameez/designer             → subcategory filter
/readymade/kurti-with-bottom        → subcategory
/premium/heavy-handwork             → subcategory
```

`CategoryPage` reads `categoryId` and optional `subPath` from React Router params.

---

## 5. Homepage Sections

`HomePage` composes these sections in order:

1. **Hero** — Full-viewport carousel, CTAs (explore collection, WhatsApp)
2. **CategoryCollage** — 6 featured category tiles
3. **TrendingSplit** — Split editorial + product highlight
4. **NewArrivalsShowcase** — Featured `new` products
5. **CampaignOverlay** — Promotional overlay/banner
6. **PremiumLine** — Premium/partywear products
7. **RetailVoices** — Testimonials
8. **TradeAssurance** — Trust/benefits strip
9. **WhatsAppStrip** — Bottom CTA band

Additional home components exist in codebase but are not all mounted on `HomePage` (e.g. `BridalCarousel`, `EditorialGrid`, `RunwaySection`) — available for reuse.

---

## 6. Data Models

### SiteConfig (`src/types/index.ts`)

```ts
interface SiteConfig {
  name: string           // "Bilal Textiles"
  slogan: string
  tagline: string        // "Wholesaler, Bulk Orders Only"
  phone: string
  whatsapp: string       // digits only, no +
  address: string
  locationNote: string
  currencies: string[]   // ["INR", "USD", "EUR", "AED", "SAR"]
  copyright: string
  hero: { eyebrow, title, subtitle, primaryCta, secondaryCta }
}
```

### Category

```ts
interface Category {
  id: string             // URL slug, e.g. "salwar-kameez"
  title: string
  href: string           // e.g. "/salwar-kameez"
  image: string
  description: string
  children: { label: string; href: string }[]  // subcategories
}
```

**11 top-level categories:** salwar-kameez, pakistani-suits, readymade, partywear, dress-material, combo, kids-wear, kaftan, coord-sets, winter, premium.

### Product

```ts
interface Product {
  id: number
  slug: string           // URL-safe, e.g. "rose-pink-kurti-pant-set"
  name: string           // Display name (often UPPERCASE in data)
  price: number          // INR, integer
  views: number          // Popularity metric for sort
  moq: number            // Minimum order quantity (typically 4)
  categoryPath: string   // e.g. "readymade/kurti-with-bottom"
  imageIndex: number     // Index into dress image pool
  featured?: "summer" | "month" | "new"
  specifications: {
    fabric: string
    bottom: string
    dupatta: string
    batchNo: string      // Inventory/batch identifier
  }
}
```

**Current catalogue:** Live active products explicitly published with `isAddedInB2B: true`, loaded through `src/lib/api.ts` and `CatalogContext`.

### Cart & User (client-side only)

```ts
interface CartLine { id: string; quantity: number } // source-aware databaseKey:productId
interface RetailerProfile { businessName: string; contactName: string; email: string; phone: string }
```

**localStorage keys:**
- `bilal_cart`
- `bilal_wishlist`
- `bilal_retailer_profile`

---

## 7. Storefront Features (Current)

### Catalog & Filtering (`src/lib/filters.ts`)

| Filter | Type |
|--------|------|
| Price range | min/max slider |
| MOQ | multi-select |
| Fabric, Bottom, Dupatta | multi-select (from product specs) |
| Subcategory | multi-select (from category children) |
| Highlights | summer / month / new |
| Sort | featured, price-asc, price-desc, popular (views), newest |

### Shopping Flow

1. Browse category → filter/sort products
2. Open product detail → set quantity (≥ MOQ)
3. Add to cart or wishlist
4. Cart → edit quantities → **Enquire via WhatsApp** (pre-filled message with items + estimated total)
5. No payment gateway, no order persistence on server

### WhatsApp Integration (`src/lib/whatsapp.ts`)

| Function | Use |
|----------|-----|
| `generalEnquiryUrl()` | Generic catalogue enquiry |
| `productEnquiryUrl(name, url)` | Single product enquiry |
| `cartEnquiryUrl(lines)` | Full cart order enquiry with total |
| `buildWhatsAppUrl(message)` | Low-level URL builder |

Phone: `site.whatsapp` → `https://wa.me/{number}?text=...`

### Auth (Demo)

- `signIn(email, password)` — accepts any non-empty email
- `register(name, email, password)` — stores name + email in localStorage
- No roles, no JWT, no protected routes

---

## 8. Design System

### Fonts
- **Display / Sans:** Hanken Grotesk
- **Accent (serif):** Cormorant Garamond

### Color Tokens (`src/index.css`)

| Token | Value | Usage |
|-------|-------|-------|
| `canvas` | `#faf7f2` | Page background |
| `canvas-warm` | `#f3ede4` | Warm sections |
| `ink` | `#1c1917` | Primary text |
| `ink-muted` | `#57534e` | Secondary text |
| `brand` | `#e91e8c` | Primary accent (pink) |
| `brand-dark` | `#c41775` | Hover states |
| `brand-soft` | `#fce4f1` | Soft highlights |
| `surface` | `#ffffff` | Cards, panels |
| `obsidian` | `#23181c` | Dark sections |
| `gold` | `#b8860b` | Accent |
| `maroon` | `#8b2942` | Accent |

### UI Patterns
- Rounded cards (`rounded-2xl`, `rounded-3xl`)
- Soft shadows (`shadow-soft`, `shadow-elevated`)
- Floating header pills with backdrop blur
- `page-x` utility for horizontal padding
- Framer Motion for page transitions and scroll reveals
- Mobile sticky bottom bar on product page (WhatsApp + Add to cart)

### Reusable UI Components
- `Button` — variants: default, outline, ghost, whatsapp; supports `to`, `href`, `onClick`
- `ProductCard`, `PageHero`, `BreadcrumbBar`
- `FadeIn`, `Stagger` — scroll animations
- `WhatsAppFab`, `BackToTopFab` — floating actions

---

## 9. Images

| Location | Files | Usage |
|----------|-------|-------|
| `public/images/dresses/` | pro-1 … pro-12.webp | Product images (cycled by `imageIndex` / `id`) |
| `public/images/hero/` | hero-01 … hero-05.webp | Hero carousel |
| `public/images/banners/` | banner-01, banner-2 | Campaign banners |
| `public/logo.png` | — | Brand logo |

Run `npm run compress:images` after adding new JPGs.

---

## 10. What Does NOT Exist Yet (Admin Integration Targets)

These are the gaps where the **billing admin panel** should plug in:

| Area | Current State | Admin Panel Opportunity |
|------|---------------|-------------------------|
| **Products CRUD** | Live read-only storefront catalog | Admin add/edit/delete products and B2B publication |
| **Categories CRUD** | Live product taxonomy plus static marketing navigation | Manage category tree and subcategories |
| **Orders** | Source-split enquiry records plus WhatsApp follow-up | Order inbox, status tracking, invoice generation |
| **Customers / Retailers** | Local profile; upserted per enquiry source | Real retailer accounts, KYC, credit terms |
| **Inventory** | Live ledger-derived availability | Stock alerts and reservation workflows |
| **Billing / Invoices** | None | Invoices, payments, GST, credit notes (from billing project) |
| **Content** | Static `site.ts`, `about.ts` | CMS for hero copy, testimonials, banners |
| **Media** | Manual file drop | Upload manager for product/hero images |
| **Analytics** | `views` count on product | Dashboard: sales, enquiries, top products |
| **Auth & Roles** | None | Admin login, staff roles, permissions |
| **API / Database** | None | REST/GraphQL backend connecting storefront + admin |

---

## 11. Suggested Admin Panel Mapping

When porting screens from the billing admin project, map them roughly as follows:

### Core Admin Routes (recommended)

```
/admin                    → Dashboard (orders, revenue, low stock)
/admin/products           → Product list (table + search + filters)
/admin/products/new       → Create product
/admin/products/:id       → Edit product
/admin/categories         → Category tree manager
/admin/orders             → Order/enquiry list (from WhatsApp or form)
/admin/orders/:id         → Order detail + invoice
/admin/customers          → Retailer accounts
/admin/invoices           → Billing module (from existing project)
/admin/invoices/:id       → Invoice detail / PDF
/admin/settings           → Site config (contact, hero, currencies)
/admin/media              → Image library
```

### Data the Admin Must Manage (maps to storefront)

| Admin Entity | Storefront Consumer |
|--------------|---------------------|
| Product | `ProductPage`, `ProductCard`, `CategoryPage`, cart |
| Category | Navigation, `CategoryPage`, breadcrumbs |
| Site settings | `site.ts` fields → Header, Footer, Contact, Hero |
| Banner/Campaign | `CampaignOverlay`, `Hero` slides |
| Testimonial | `RetailVoices` section |
| Order | Cart WhatsApp flow → formal order record |
| Invoice | Post-order billing (billing project) |
| Retailer/User | `LoginPage` / `RegisterPage` (replace demo auth) |

### Product Fields to Expose in Admin Form

- Name, slug (auto-generate from name)
- Price (INR)
- MOQ
- Category + subcategory (dropdown from category tree)
- Specifications: fabric, bottom, dupatta, batchNo
- Featured flag: none / summer / month / new
- Images (upload or select from media library)
- Active/hidden toggle
- Views (read-only or manual)

---

## 12. Integration Architecture Options

### Option A — Monorepo / Same Repo
Add `src/admin/` with separate route tree under `/admin/*`, shared types in `src/types/`, shared API client in `src/lib/api/`.

```
App.tsx
├── /admin/*     → AdminLayout + admin pages (from billing project)
└── /*           → Storefront Layout (existing)
```

### Option B — Separate Admin App
Keep billing admin as separate Vite app; share types package or OpenAPI-generated client. Storefront and admin both call same backend.

### Option C — Phase 1 Static Admin
Replace `src/data/*.ts` with JSON files loaded from admin-exported API; minimal backend initially.

**Recommended:** Option A or B with a real API — the storefront is already structured for clean type boundaries (`types/`, `lib/data.ts` accessors).

---

## 13. Storefront ↔ Admin Data Flow (Target)

```
┌─────────────────┐     API      ┌─────────────────┐
│  Admin Panel    │ ──────────►  │    Backend      │
│  (billing UI)   │ ◄──────────  │  (DB + storage) │
└─────────────────┘              └────────┬────────┘
                                          │ API
                                          ▼
                                 ┌─────────────────┐
                                 │  Storefront     │
                                 │  (this project) │
                                 └─────────────────┘
```

Replace direct imports in `lib/data.ts`:

```ts
// Today:
import { products } from '@/data/products'

// Target:
const products = await api.getProducts()
```

Keep the same `Product`, `Category` interfaces so UI components need minimal changes.

---

## 14. Key Files Reference (for admin work)

| Concern | File(s) |
|---------|---------|
| Routes | `src/App.tsx` |
| Product types | `src/types/index.ts` |
| Product API/data | `src/lib/api.ts`, `src/context/CatalogContext.tsx` |
| Category data | `src/data/categories.ts` |
| Data accessors | `src/lib/data.ts` |
| Cart/user state | `src/context/ShopContext.tsx` |
| Filters logic | `src/lib/filters.ts` |
| Site config | `src/data/site.ts` |
| Feature flags | `src/lib/featureFlags.ts` |
| Design tokens | `src/index.css` |
| Button component | `src/components/ui/Button.tsx` |

---

## 15. Business Rules to Preserve

1. **MOQ enforcement** — Cart and product page enforce minimum quantity per product.
2. **Wholesale positioning** — Copy and UX emphasize bulk/retailer, not D2C checkout.
3. **WhatsApp as primary CTA** — Even with admin orders, WhatsApp enquiry may remain a channel.
4. **INR pricing** — `formatPrice()` uses `en-IN` locale; multi-currency is config-only today.
5. **Batch numbers** — `specifications.batchNo` is used for inventory identification; admin should treat as SKU/batch key.
6. **Coming soon gate** — Production may hide full site until `VITE_COMING_SOON=false`.

---

## 16. Contact & Business Info (from `site.ts`)

| Field | Value |
|-------|-------|
| Name | Bilal Textiles |
| Phone | +91 7093175229 |
| WhatsApp | 917093175229 |
| Address | Third floor, above Bank of Baroda, Chatta Bazaar, Dewan Devdi, Purani Haveli, Pathar Gatti, Hyderabad, Telangana-500002 |
| Markets | Surat Textile Market & Hyderabad |

---

## 17. Checklist for Admin Incorporation

- [ ] Audit billing admin screens → map to sections in §11
- [ ] Align `Product` / `Category` types between admin and storefront
- [ ] Add `/admin` route tree with auth guard
- [x] Connect public multi-database storefront API
- [x] Migrate product catalog reads to database-backed offers
- [ ] Wire admin product form to `Product` schema (incl. specs, MOQ, batchNo)
- [ ] Port invoice/billing modules from billing project
- [ ] Replace `ShopContext` demo auth with real retailer auth (optional phase)
- [ ] Image upload → `public/images/` or cloud storage
- [x] Capture source-split in-app enquiries; retain WhatsApp follow-up
- [ ] Match admin UI theme to brand tokens (§8) or keep admin on neutral dashboard theme
- [ ] Disable `VITE_COMING_SOON` when ready for public launch

---

*Last updated: June 2026 — generated from codebase snapshot of bilal-textiles v1.0.0*
