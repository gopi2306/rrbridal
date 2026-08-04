# WhatsApp customer promotion broadcast

Send marketing promotions via Meta template **`promo_offer`** (separate from billing `invoice_send`).

## Meta template shape (matches Graph send)

| Component | Value |
|-----------|--------|
| Body named `customer_name` | Customer name |
| Body named `discount` | Offer (e.g. `20`) |
| Body named `product_offer` | Scope (e.g. `All Products`) |
| Body named `expiry_date` | Date (e.g. `31 July 2026`) |
| URL button | **Static** on this WABA — do **not** send button parameters (`promoHasUrlButton: false`) |

This account’s `promo_offer` uses **named** parameters (not `{{1}}`…`{{4}}` positional). Sending positional params causes `(#100) Parameter name is missing or empty`.

Working Graph body:

```json
{
  "type": "body",
  "parameters": [
    { "type": "text", "parameter_name": "customer_name", "text": "John" },
    { "type": "text", "parameter_name": "discount", "text": "20" },
    { "type": "text", "parameter_name": "product_offer", "text": "All Products" },
    { "type": "text", "parameter_name": "expiry_date", "text": "31 July 2026" }
  ]
}
```

## Store settings (save, then run from WPF)

```bash
curl -X PATCH "http://localhost:3000/api/admin/stores/store-001/whatsapp-settings" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "promoTemplateName": "promo_offer",
    "promoTemplateLanguage": "en",
    "promoBodyParamMode": "name_offer_scope_date",
    "promoHasUrlButton": false,
    "promoHeaderType": "none"
  }'
```

Do **not** change billing fields (`templateName` / `attachmentType`) unless you intend to move invoice to the same number.

## APIs

### Start broadcast (JSON body)

`POST /api/whatsapp/broadcast/json`

```bash
curl -X POST "http://localhost:3000/api/whatsapp/broadcast/json" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "storeId": "store-001",
    "mode": "template",
    "promoText": "20",
    "offerScope": "All Products",
    "offerDate": "07 August 2026",
    "recipients": [
      { "name": "gopi", "phone": "9080405673" }
    ]
  }'
```

Returns `{ "jobId": "..." }`.

### Start broadcast (multipart or JSON)

`POST /api/whatsapp/broadcast` — same fields; multipart supports optional `attachment` + `recipientsJson`.

### Job status

`GET /api/whatsapp/broadcast/:jobId` — `status`, counts, per-recipient results.

**Important:** `status: "sent"` / `sent: 1` means Meta **accepted** the API call (`wamid`). That is **not** proof the phone received the message.

After webhooks are configured, each result also has:

- `deliveryStatus`: `sent` → `delivered` → `read`, or `failed`
- `deliveryError`: Meta reason when delivery fails (e.g. marketing limits)

### Delivery webhooks (required to know why phone shows nothing)

1. Expose central API publicly (ngrok / production HTTPS).
2. Meta App → WhatsApp → **Configuration** → Webhook:
   - Callback URL: `https://YOUR_PUBLIC_HOST/api/whatsapp/webhook`
   - Verify token: same as `WHATSAPP_WEBHOOK_VERIFY_TOKEN` in `.env` (default `rr-bridal-wa-verify`)
3. Subscribe to **messages** (includes status updates).
4. Restart backend, send again, then re-check `GET /api/whatsapp/broadcast/:jobId` for `deliveryStatus` / `deliveryError`.

Also check WhatsApp on `9080405673` for chat from **Trugotech (+91 96003 87958)**, including Business / Spam.

## WPF

**Customers** → select → **Broadcast WhatsApp** → Marketing template:

- Offer (`discount`), Scope (`product_offer`), Date (`expiry_date`)

## Related

- [whatsapp-bill-delivery.md](./whatsapp-bill-delivery.md) — invoice WhatsApp (separate)

