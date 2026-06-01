using MongoDB.Bson;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services.Billing.Promotions;

public sealed class PromotionSchemeRepository
{
    private readonly IMongoCollection<BsonDocument> _schemes;

    public PromotionSchemeRepository(IMongoDatabase localDb)
    {
        _schemes = localDb.GetCollection<BsonDocument>("local_promotion_schemes");
    }

    public async Task<IReadOnlyList<PromotionSchemeDefinition>> LoadActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("isActive", true);
        var docs = await _schemes.Find(filter).ToListAsync(ct);
        return docs.Select(Map).Where(s => s.IsActive).OrderBy(s => s.Priority).ToList();
    }

    public IReadOnlyList<PromotionSchemeDefinition> LoadActive()
    {
        var filter = Builders<BsonDocument>.Filter.Eq("isActive", true);
        return _schemes.Find(filter).ToList().Select(Map).Where(s => s.IsActive).OrderBy(s => s.Priority).ToList();
    }

    public static PromotionSchemeDefinition Map(BsonDocument doc)
    {
        var benefitDoc = doc.GetValue("benefit", new BsonDocument()).AsBsonDocument;
        var condDoc = doc.GetValue("conditions", new BsonDocument()).AsBsonDocument;

        var slabs = new List<PromotionSlab>();
        if (benefitDoc.TryGetValue("slabs", out var slabsVal) && slabsVal.IsBsonArray)
        {
            foreach (var s in slabsVal.AsBsonArray)
            {
                if (!s.IsBsonDocument) continue;
                var sd = s.AsBsonDocument;
                slabs.Add(new PromotionSlab
                {
                    FromAmount = ReadDecimal(sd, "fromAmount"),
                    ToAmount = sd.Contains("toAmount") ? ReadDecimal(sd, "toAmount") : null,
                    DiscountPercent = ReadDecimal(sd, "discountPercent"),
                });
            }
        }

        var comboSkus = ReadStringArray(benefitDoc, "comboSkus");
        var timeWindows = new List<PromotionTimeWindow>();
        if (doc.TryGetValue("timeWindows", out var twVal) && twVal.IsBsonArray)
        {
            foreach (var w in twVal.AsBsonArray)
            {
                if (!w.IsBsonDocument) continue;
                var wd = w.AsBsonDocument;
                timeWindows.Add(new PromotionTimeWindow
                {
                    DayOfWeek = wd.GetValue("dayOfWeek", 0).ToInt32(),
                    FromHour = wd.GetValue("fromHour", 0).ToInt32(),
                    ToHour = wd.GetValue("toHour", 0).ToInt32(),
                });
            }
        }

        var requiredSkus = new List<PromotionComboRequirement>();
        if (condDoc.TryGetValue("requiredSkus", out var reqVal) && reqVal.IsBsonArray)
        {
            foreach (var r in reqVal.AsBsonArray)
            {
                if (!r.IsBsonDocument) continue;
                var rd = r.AsBsonDocument;
                requiredSkus.Add(new PromotionComboRequirement
                {
                    Sku = rd.GetValue("sku", "").AsString,
                    RequiredQty = ReadDecimal(rd, "requiredQty"),
                });
            }
        }

        return new PromotionSchemeDefinition
        {
            Id = doc.GetValue("schemeId", doc.GetValue("_id", "")).ToString() ?? "",
            Code = doc.GetValue("code", "").AsString,
            Name = doc.GetValue("name", "").AsString,
            Kind = doc.GetValue("kind", "scheme").AsString,
            Type = doc.GetValue("type", "").AsString,
            Priority = doc.GetValue("priority", 100).ToInt32(),
            IsActive = doc.GetValue("isActive", true).ToBoolean(),
            Stacking = doc.GetValue("stacking", "best_benefit").AsString,
            StoreIds = ReadStringArray(doc, "storeIds"),
            ValidFrom = ReadNullableDate(doc, "validFrom"),
            ValidTo = ReadNullableDate(doc, "validTo"),
            TimeWindows = timeWindows,
            Conditions = new PromotionConditions
            {
                Skus = ReadStringArray(condDoc, "skus"),
                CategoryIds = ReadStringArray(condDoc, "categoryIds"),
                BrandIds = ReadStringArray(condDoc, "brandIds"),
                OfferGroupIds = ReadStringArray(condDoc, "offerGroupIds"),
                MinLineQty = condDoc.Contains("minLineQty") ? ReadDecimal(condDoc, "minLineQty") : null,
                MinBillAmount = condDoc.Contains("minBillAmount") ? ReadDecimal(condDoc, "minBillAmount") : null,
                CustomerTypes = ReadStringArray(condDoc, "customerTypes"),
                CustomerCodes = ReadStringArray(condDoc, "customerCodes"),
                RequiredSkus = requiredSkus,
            },
            Benefit = new PromotionBenefit
            {
                Mode = benefitDoc.GetValue("mode", "").AsString,
                BuyQty = ReadDecimal(benefitDoc, "buyQty"),
                GetQty = ReadDecimal(benefitDoc, "getQty"),
                FreeOn = benefitDoc.GetValue("freeOn", "cheapest").AsString,
                DiscountPercent = ReadDecimal(benefitDoc, "discountPercent"),
                FlatAmount = ReadDecimal(benefitDoc, "flatAmount"),
                MinBillAmount = ReadDecimal(benefitDoc, "minBillAmount"),
                FixedPrice = ReadDecimal(benefitDoc, "fixedPrice"),
                ComboSkus = comboSkus,
                Slabs = slabs,
            },
        };
    }

    private static decimal ReadDecimal(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull) return 0;
        return v.BsonType switch
        {
            BsonType.Double => (decimal)v.AsDouble,
            BsonType.Int32 => v.AsInt32,
            BsonType.Int64 => v.AsInt64,
            BsonType.Decimal128 => (decimal)v.AsDecimal,
            _ => 0,
        };
    }

    private static DateTime? ReadNullableDate(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v.IsBsonNull) return null;
        if (v.IsValidDateTime) return v.ToUniversalTime();
        if (v.IsString && DateTime.TryParse(v.AsString, out var dt)) return dt.ToUniversalTime();
        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || !v.IsBsonArray) return Array.Empty<string>();
        return v.AsBsonArray
            .Where(x => x.IsString && !string.IsNullOrWhiteSpace(x.AsString))
            .Select(x => x.AsString.Trim())
            .ToList();
    }
}
