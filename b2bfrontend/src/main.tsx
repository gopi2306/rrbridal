import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { ShopProvider } from '@/context/ShopContext'
import { CatalogProvider } from '@/context/CatalogContext'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <CatalogProvider>
      <ShopProvider>
        <App />
      </ShopProvider>
    </CatalogProvider>
  </StrictMode>,
)
