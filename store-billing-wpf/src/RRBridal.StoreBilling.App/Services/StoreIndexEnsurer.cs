using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>Creates indexes used by multi-counter reporting on shared MongoDB.</summary>
public static class StoreIndexEnsurer
{
    public static async Task EnsureAsync(IMongoDatabase db, CancellationToken ct = default)
    {
        var bills = db.GetCollection<MongoDB.Bson.BsonDocument>("store_bills");
        var keys = Builders<MongoDB.Bson.BsonDocument>.IndexKeys
            .Ascending("storeId")
            .Ascending("deviceId")
            .Descending("createdAtUtc");
        await bills.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(keys, new CreateIndexOptions { Name = "storeId_deviceId_createdAtUtc" }),
            cancellationToken: ct);

        await bills.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("billNo"),
                new CreateIndexOptions { Name = "billNo" }),
            cancellationToken: ct);

        await bills.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Descending("createdAtUtc"),
                new CreateIndexOptions { Name = "createdAtUtc_desc" }),
            cancellationToken: ct);

        var creditNotes = db.GetCollection<MongoDB.Bson.BsonDocument>("customer_credit_notes");
        await creditNotes.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("customerPhoneNorm")
                    .Ascending("status"),
                new CreateIndexOptions { Name = "storeId_phone_status" }),
            cancellationToken: ct);
        await creditNotes.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("creditNoteNo"),
                new CreateIndexOptions { Name = "creditNoteNo", Unique = true }),
            cancellationToken: ct);
        await creditNotes.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("originalBillNo"),
                new CreateIndexOptions
                {
                    Name = "storeId_originalBillNo_unique",
                    Unique = true,
                    Sparse = true,
                }),
            cancellationToken: ct);

        var holds = db.GetCollection<MongoDB.Bson.BsonDocument>("held_bills");
        await holds.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("holdNo"),
                new CreateIndexOptions { Name = "storeId_holdNo", Unique = true }),
            cancellationToken: ct);
        await holds.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Descending("updatedAtUtc"),
                new CreateIndexOptions { Name = "storeId_updatedAtUtc" }),
            cancellationToken: ct);

        var quotations = db.GetCollection<MongoDB.Bson.BsonDocument>("store_quotations");
        await quotations.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("quotationNo"),
                new CreateIndexOptions { Name = "storeId_quotationNo", Unique = true }),
            cancellationToken: ct);
        await quotations.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Descending("updatedAtUtc"),
                new CreateIndexOptions { Name = "storeId_quotation_updatedAtUtc" }),
            cancellationToken: ct);

        var paymentReceipts = db.GetCollection<MongoDB.Bson.BsonDocument>("store_payment_receipts");
        await paymentReceipts.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("receiptNo"),
                new CreateIndexOptions { Name = "storeId_receiptNo", Unique = true }),
            cancellationToken: ct);
        await paymentReceipts.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("billNo")
                    .Descending("createdAtUtc"),
                new CreateIndexOptions { Name = "storeId_billNo_createdAtUtc" }),
            cancellationToken: ct);

        var cnCashouts = db.GetCollection<MongoDB.Bson.BsonDocument>("store_credit_note_cashouts");
        await cnCashouts.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Descending("createdAtUtc"),
                new CreateIndexOptions { Name = "storeId_createdAtUtc" }),
            cancellationToken: ct);
        await cnCashouts.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("cashoutNo"),
                new CreateIndexOptions { Name = "cashoutNo", Unique = true }),
            cancellationToken: ct);

        var saleReturns = db.GetCollection<MongoDB.Bson.BsonDocument>("store_sale_returns");
        try
        {
            await saleReturns.Indexes.DropOneAsync("storeId_originalBillNo_unique", cancellationToken: ct);
        }
        catch (MongoCommandException)
        {
            // Index may not exist on older installs.
        }

        await saleReturns.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("originalBillNo"),
                new CreateIndexOptions
                {
                    Name = "storeId_originalBillNo",
                }),
            cancellationToken: ct);

        var dailyExpenses = db.GetCollection<MongoDB.Bson.BsonDocument>("store_daily_expenses");
        await dailyExpenses.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("expenseNo"),
                new CreateIndexOptions
                {
                    Name = "storeId_expenseNo_unique",
                    Unique = true,
                }),
            cancellationToken: ct);
        await dailyExpenses.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("businessDate"),
                new CreateIndexOptions { Name = "storeId_businessDate" }),
            cancellationToken: ct);

        var auditLogs = db.GetCollection<MongoDB.Bson.BsonDocument>("store_audit_logs");
        await auditLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Descending("createdAtUtc"),
                new CreateIndexOptions { Name = "storeId_createdAtUtc" }),
            cancellationToken: ct);
        await auditLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("entityType")
                    .Ascending("entityId")
                    .Descending("createdAtUtc"),
                new CreateIndexOptions { Name = "entityType_entityId_createdAtUtc" }),
            cancellationToken: ct);
        await auditLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("sku"),
                new CreateIndexOptions { Name = "sku" }),
            cancellationToken: ct);

        var daySessions = db.GetCollection<MongoDB.Bson.BsonDocument>("store_day_sessions");
        await daySessions.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("businessDate")
                    .Ascending("posCounter"),
                new CreateIndexOptions
                {
                    Name = "storeId_businessDate_posCounter_unique",
                    Unique = true,
                }),
            cancellationToken: ct);
        await daySessions.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("businessDate")
                    .Ascending("status"),
                new CreateIndexOptions { Name = "storeId_businessDate_status" }),
            cancellationToken: ct);

        var cashMovements = db.GetCollection<MongoDB.Bson.BsonDocument>("store_cash_movements");
        await cashMovements.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("movementNo"),
                new CreateIndexOptions
                {
                    Name = "storeId_movementNo_unique",
                    Unique = true,
                }),
            cancellationToken: ct);
        await cashMovements.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                Builders<MongoDB.Bson.BsonDocument>.IndexKeys
                    .Ascending("storeId")
                    .Ascending("businessDate"),
                new CreateIndexOptions { Name = "storeId_businessDate" }),
            cancellationToken: ct);
    }
}
