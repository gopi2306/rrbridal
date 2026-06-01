using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services.Billing.Promotions;
using Xunit;

namespace RRBridal.StoreBilling.Tests;

/// <summary>
/// Verifies sync-stored scheme documents (typed PromotionBenefit from central) map correctly for the engine.
/// </summary>
public class PromotionSchemeRepositoryTests
{
    [Fact]
    public void Map_TypedItemBenefit_FromSyncDocument()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "schemeId": "abc123",
              "code": "sku001-bxgy",
              "name": "SKU-001 Buy 2 Get 1",
              "kind": "scheme",
              "type": "item",
              "priority": 10,
              "isActive": true,
              "stacking": "best_benefit",
              "storeIds": [],
              "conditions": { "skus": ["SKU-001"], "minLineQty": 3 },
              "benefit": {
                "mode": "buy_x_get_y",
                "buyQty": 2,
                "getQty": 1,
                "freeOn": "cheapest",
                "slabs": [],
                "comboSkus": []
              }
            }
            """);

        var scheme = PromotionSchemeRepository.Map(doc);

        Assert.Equal("sku001-bxgy", scheme.Code);
        Assert.Equal("scheme", scheme.Kind);
        Assert.Equal("item", scheme.Type);
        Assert.Equal("buy_x_get_y", scheme.Benefit.Mode);
        Assert.Equal(2m, scheme.Benefit.BuyQty);
        Assert.Equal(1m, scheme.Benefit.GetQty);
        Assert.Equal("cheapest", scheme.Benefit.FreeOn);
        Assert.Empty(scheme.Benefit.Slabs);
        Assert.Empty(scheme.Benefit.ComboSkus);
        Assert.Contains("SKU-001", scheme.Conditions.Skus);
    }

    [Fact]
    public void Map_TypedSlabBenefit_FromSyncDocument()
    {
        var doc = BsonDocument.Parse(
            """
            {
              "code": "slab-tier",
              "kind": "scheme",
              "type": "slab",
              "priority": 40,
              "isActive": true,
              "stacking": "best_benefit",
              "conditions": {},
              "benefit": {
                "slabs": [
                  { "fromAmount": 3000, "toAmount": 5000, "discountPercent": 10 }
                ],
                "comboSkus": []
              }
            }
            """);

        var scheme = PromotionSchemeRepository.Map(doc);

        Assert.Equal("slab", scheme.Type);
        Assert.Single(scheme.Benefit.Slabs);
        Assert.Equal(3000m, scheme.Benefit.Slabs[0].FromAmount);
        Assert.Equal(5000m, scheme.Benefit.Slabs[0].ToAmount);
        Assert.Equal(10m, scheme.Benefit.Slabs[0].DiscountPercent);
    }
}
