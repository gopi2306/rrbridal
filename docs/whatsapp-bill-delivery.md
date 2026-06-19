# WhatsApp bill delivery

Send posted store bills to customers via **WhatsApp Business API**. The WPF app generates a thermal receipt PNG and central backend uploads it to Meta and sends an approved template message.

## Prerequisites

1. Meta WhatsApp Business account (WABA) with a verified phone number
2. Approved template with **image header** and body variables, e.g.:
   - `Hello {{1}}, thank you for your purchase at {{2}}. Invoice {{3}} — {{4}}.`
3. Graph API access token with `whatsapp_business_messaging` permission

## Central admin configuration

Configure per store via admin API (requires JWT):

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

## WPF settings

**Settings → WhatsApp**

- **Auto-send bill on post** — after F9 post, sends when customer phone is present and central WhatsApp is enabled
- **Test send** — sample thermal PNG to a test mobile
- Credentials are managed in Central admin (not stored on the till)

## Send invoice API

`POST /api/whatsapp/send-invoice` (multipart, JWT required)

| Field | Description |
|-------|-------------|
| `storeId` | Store code |
| `billNo` | Posted bill number |
| `customerName` | Customer name |
| `customerPhone` | 10-digit or E.164 |
| `payable` | Bill amount |
| `attachment` | PNG file (thermal receipt) |

```bash
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

- [sync-protocol.md](./sync-protocol.md) — store billing sync (WhatsApp does not use a separate sync event in v1)
