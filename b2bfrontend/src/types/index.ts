export interface Breadcrumb {
  label: string;
  href?: string;
}

export interface SiteConfig {
  name: string;
  slogan: string;
  tagline: string;
  phone: string;
  whatsapp: string;
  address: string;
  locationNote: string;
  currencies: string[];
  copyright: string;
  hero: {
    eyebrow: string;
    title: string;
    subtitle: string;
    primaryCta: string;
    secondaryCta: string;
  };
}

export interface NavChild {
  label: string;
  href: string;
}

export interface Category {
  id: string;
  title: string;
  href: string;
  image: string;
  description: string;
  children: NavChild[];
}

export interface StorefrontOffer {
  id: string
  databaseKey: string
  databaseLabel: string
  productId: string
  sku: string
  slug: string
  name: string
  price: number
  mrp: number | null
  moq: number
  category: { key: string; name: string }
  subCategory: { key?: string; name: string } | string | null
  images: string[]
  stock: {
    total: number
    warehouse: number
    store: number
    inTransit: number
    available: boolean
  }
  specifications: Record<string, string>
}

export interface Product extends StorefrontOffer {
  /** Compatibility fields used by existing presentation/filter components. */
  views: number
  categoryPath: string
  featured?: 'summer' | 'month' | 'new'
}

export interface StorefrontCategory {
  key: string
  name: string
  productCount: number
}

export interface RetailerProfile {
  businessName: string
  contactName: string
  email: string
  phone: string
  address?: string
  city?: string
  state?: string
  pincode?: string
}

export interface Testimonial {
  id: number;
  name: string;
  text: string;
  role?: string;
}

export interface AboutContent {
  intro: string;
  mission: string;
  highlights: { title: string; desc: string }[];
  values: { title: string; desc: string }[];
}
