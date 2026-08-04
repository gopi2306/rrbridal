import fs from 'node:fs'

const res = await fetch('https://bilaltextiles.trugotech.com/assets/index-BgAcRwt4.js')
const js = await res.text()

function slugify(name) {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
}

function inferCategory(name) {
  const n = name.toLowerCase()
  if (n.includes('sharara') || n.includes('gharara') || n.includes('garara')) return 'partywear/garara-sharara'
  if (n.includes('organza') || n.includes('jacquard')) return 'pakistani-suits/dress-materials'
  if (n.includes('handwork') || n.includes('premium') || n.includes('silk')) return 'premium/heavy-handwork'
  if (n.includes('palazzo') || n.includes('kurti') || n.includes('patiala')) return 'readymade/kurti-with-bottom'
  if (n.includes('co-ord') || n.includes('coord') || n.includes('floral')) return 'coord-sets/premium-party'
  return 'readymade/designer-kurti'
}

function categoryImageIndex(categoryPath) {
  const id = categoryPath.split('/')[0]
  const map = {
    'salwar-kameez': 0, 'pakistani-suits': 1, readymade: 2, partywear: 3,
    'dress-material': 4, combo: 5, 'kids-wear': 6, kaftan: 7,
    'coord-sets': 8, winter: 9, premium: 10,
  }
  return map[id] ?? 2
}

const blocks = [...js.matchAll(/\{id:(\d+),name:`([^`]+)`,price:(\d+),views:(\d+),moq:(\d+)\}/g)]
const seen = new Set()
const products = []

for (const [, id, name, price, views, moq] of blocks) {
  const slug = slugify(name)
  if (seen.has(slug) || !name.trim() || Number(price) < 100) continue
  seen.add(slug)
  const categoryPath = inferCategory(name)
  products.push({
    id: products.length + 1,
    slug,
    name,
    price: Number(price),
    views: Number(views),
    moq: Number(moq),
    categoryPath,
    imageIndex: categoryImageIndex(categoryPath),
    specifications: {
      fabric: 'Premium Blend',
      bottom: 'Crepe',
      dupatta: 'Net',
      batchNo: String(10000 + Number(id)),
    },
  })
}

products.slice(0, 5).forEach((p) => { p.featured = 'summer' })
products.slice(5, 8).forEach((p) => { p.featured = 'month' })
products.slice(8, 20).forEach((p) => { p.featured = 'new' })

const out = `import type { Product } from '@/types'\n\nexport const products: Product[] = ${JSON.stringify(products, null, 2)}\n`
fs.writeFileSync('src/data/products.ts', out)
console.log(`Wrote ${products.length} products from Bilal team site`)
