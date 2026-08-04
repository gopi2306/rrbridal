# WhatsApp bill delivery

Send posted store bills to customers via **WhatsApp Business API**. The WPF app builds the selected invoice layout (Thermal / A4 / A5 / A4 commercial) as a PNG or PDF and central backend uploads it to Meta and sends an approved template message.

## Prerequisites

1. Meta WhatsApp Business account (WABA) with a verified phone number
2. Approved template matching the store `attachmentType`:
   - **image** (default): image header + 4 body variables, e.g.  
     `Hello {{1}}, thank you for your purchase at {{2}}. Invoice {{3}} — {{4}}.`
   - **document** (PDF): document header + 2 body variables, e.g. template `invoice_send`  
     `Hello {{1}}, your invoice {{2}} is attached.`
3. Graph API access token with `whatsapp_business_messaging` permission (System User permanent token for the same WABA)
4. Correct **Phone number ID** from Meta → WhatsApp → API Setup (this is not the WABA id and not the business phone digits)

List phone numbers for a token:

```bash
curl "https://graph.facebook.com/v25.0/me?fields=id" -H "Authorization: Bearer $META_TOKEN"
# Then with your WABA id:
curl "https://graph.facebook.com/v25.0/YOUR_WABA_ID/phone_numbers" -H "Authorization: Bearer $META_TOKEN"
```

Set `WHATSAPP_GRAPH_VERSION=v25.0` in central `.env` if needed (default is now `v25.0`).

## Central admin configuration

Configure per store via admin API (requires JWT):

### PDF document template (`invoice_send`)

```bash
curl -X PATCH "http://localhost:3000/api/admin/stores/store-001/whatsapp-settings" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "phoneNumberId": "YOUR_PHONE_NUMBER_ID",
    "accessToken": "YOUR_PERMANENT_TOKEN",
    "templateName": "invoice_send",
    "templateLanguage": "en",
    "defaultCountryCode": "91",
    "attachmentType": "document"
  }'
```

> Use the **exact** name and language from Meta (this template is `invoice_send` / `en`). Error `#132001` means the stored pair does not match an approved template on that phone number’s WABA.

### Image template (legacy)

```bash
curl -X PATCH "http://localhost:3000/api/admin/stores/store-001/whatsapp-settings" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "phoneNumberId": "YOUR_PHONE_NUMBER_ID",
    "accessToken": "YOUR_PERMANENT_TOKEN",
    "templateName": "invoice_delivery",
    "templateLanguage": "en",
    "defaultCountryCode": "91",
    "attachmentType": "image"
  }'
```

For local dev, you may set `WHATSAPP_DEFAULT_ACCESS_TOKEN` in central `.env` instead of storing the token in Mongo.

Read masked settings (store API):

```bash
curl "http://localhost:3000/api/whatsapp/settings?storeId=store-001" \
  -H "Authorization: Bearer $TOKEN"
```

## How PDF send works

1. WPF builds the **selected** invoice layout (Settings → WhatsApp → Bill format) as a multi-page PDF when needed (`Invoice_{billNo}.pdf`). Pre-printed stationery modes are not used — customers get the full A4/A5 layout.
2. `POST /api/whatsapp/send-invoice` uploads the PDF to Meta media (`application/pdf`)
3. Central sends the template with a **document** header (`document.id` + filename) — no public invoice URL is required
4. Body params for document mode: `{{1}}` customer name, `{{2}}` bill number

When Central `attachmentType` is **image**, WPF always sends a **thermal PNG** (Meta image headers cannot carry A4 PDFs).

## WPF settings

**Settings → WhatsApp**

- **Bill format to send** — Thermal / A4 tax invoice / A5 tax invoice / A4 commercial (local till preference; defaults to Printing format on first use)
- **Auto-send bill on post** — after F9 post, sends when customer phone is present and central WhatsApp is enabled
- **Test send** — sample bill using the selected format
- Credentials are managed in Central admin (not stored on the till)
- Local file: `%LocalAppData%\RRBridal\StoreBilling\whatsapp_settings.json` (`autoSendAfterPost`, `invoiceFormat`)

## Test send API

`POST /api/whatsapp/test` (multipart, JWT required)

| Field | Description |
|-------|-------------|
| `storeId` | Optional store code |
| `customerPhone` | Destination mobile |
| `customerName` | Optional; default `Test Customer` |
| `attachment` | Optional PDF/PNG — if omitted, central generates a sample file from store `attachmentType` |

```bash
# No file upload (sample PDF/PNG generated server-side)
curl -X POST "http://localhost:3000/api/whatsapp/test" \
  -H "Authorization: Bearer $TOKEN" \
  -F "storeId=store-001" \
  -F "customerPhone=9876543210" \
  -F "customerName=Sample"
```

## Send invoice API

`POST /api/whatsapp/send-invoice` (multipart, JWT required)

| Field | Description |
|-------|-------------|
| `storeId` | Store code |
| `billNo` | Posted bill number |
| `customerName` | Customer name |
| `customerPhone` | 10-digit or E.164 |
| `payable` | Bill amount |
| `attachment` | PNG (`image`) or PDF (`document`) file |

```bash
# Document / PDF
curl -X POST "http://localhost:3000/api/whatsapp/send-invoice" \
  -H "Authorization: Bearer $TOKEN" \
  -F "storeId=store-001" \
  -F "billNo=20260617-001-01-0001" \
  -F "customerName=Sample" \
  -F "customerPhone=9876543210" \
  -F "payable=1500" \
  -F "attachment=@Invoice_20260617-001-01-0001.pdf;type=application/pdf"

# Image / PNG
curl -X POST "http://localhost:3000/api/whatsapp/send-invoice" \
  -H "Authorization: Bearer $TOKEN" \
  -F "storeId=store-001" \
  -F "billNo=20260617-001-01-0001" \
  -F "customerName=Sample" \
  -F "customerPhone=9876543210" \
  -F "payable=1500" \
  -F "attachment=@bill.png;type=image/png"
```

## Bill document fields

Local `store_bills.whatsapp`:

| Field | Values |
|-------|--------|
| `status` | `sent`, `failed`, `skipped` |
| `sentAtUtc` | ISO timestamp |
| `messageId` | Meta message id when sent |
| `phone` | E.164 destination |
| `error` | Failure reason |

## Manual resend

**Duplicate print → Bill** tab: **Send WhatsApp bill** on a posted bill with customer phone.

## Related

- [whatsapp-bill-delivery.md](./whatsapp-bill-delivery.md) — billing invoice WhatsApp (PDF/image); separate from promo broadcast
- [whatsapp-promo-broadcast.md](./whatsapp-promo-broadcast.md) — customer promotion broadcast
