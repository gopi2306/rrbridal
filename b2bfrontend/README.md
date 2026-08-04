# B2B Storefront

React/Vite wholesale storefront backed by the multi-database B2B API.

## Run locally

Start the API first:

```powershell
cd ..\b2b-backend
Copy-Item .env.example .env
# Set B2B_DATABASES and a B2B_ADMIN_API_KEY of at least 24 characters.
npm run start:dev
```

Then start the storefront:

```powershell
cd ..\b2bfrontend
Copy-Item .env.example .env.local
npm install
npm run dev
```

The Vite development server proxies `/api` to `http://127.0.0.1:3100`. For a deployed API, set:

```env
VITE_B2B_API_URL=https://b2b-api.example.com/api
```

## Publishing products

Products are read from every enabled database and remain separate business-labelled offers. A central product appears only when:

```json
{
  "isActive": true,
  "isAddedInB2B": true
}
```

Publish through the central product create/update API or import the `isAddedInB2B` Excel column. Relative product media needs `publicApiBaseUrl` on the matching `B2B_DATABASES` entry.

## Retailer enquiries

There is no password login. The retailer profile, cart, and wishlist are stored in the browser. Checkout posts one source-aware enquiry; the API validates live price and stock, then writes one enquiry and customer record to each represented database. Failed business groups remain in the cart for retry. WhatsApp is an optional follow-up.

## Build

```powershell
npm run build
```
