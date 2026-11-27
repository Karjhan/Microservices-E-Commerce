using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace IntegrationTests.Endpoints;

/// <summary>US4: stock changes announced by Inventory move a product between Active and OutOfStock (FR-016).</summary>
public class InventoryStatusSyncTests : IntegrationTestBase
{
    public InventoryStatusSyncTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Selling_Out_Marks_An_Active_Product_OutOfStock_And_Restocking_Restores_It()
    {
        var id = await ProductApi.CreateActiveProductAsync(Client);

        await PublishUntilAsync(id, availableQuantity: 0, expectedStatus: "OutOfStock");
        await PublishUntilAsync(id, availableQuantity: 7, expectedStatus: "Active");
    }

    [Fact]
    public async Task Stock_Never_Changes_The_Status_Of_A_Draft_Or_Inactive_Product()
    {
        var draft = await ProductApi.CreateProductAsync(Client);
        var inactive = await ProductApi.CreateActiveProductAsync(Client);
        (await ProductApi.SetStatusAsync(Client, inactive, "Inactive")).EnsureSuccessStatusCode();
        var sentinel = await ProductApi.CreateActiveProductAsync(Client);

        // Messages are handled in order, so once the sentinel flipped the earlier ones have been looked at too.
        await PublishAsync(draft, 10);
        await PublishAsync(inactive, 10);
        await PublishAsync(draft, 0);
        await PublishAsync(inactive, 0);
        await PublishUntilAsync(sentinel, 0, "OutOfStock");

        (await StatusAsync(draft)).Should().Be("Draft");
        (await StatusAsync(inactive)).Should().Be("Inactive");
    }

    [Fact]
    public async Task An_Update_For_An_Unknown_Product_Is_Ignored()
    {
        var sentinel = await ProductApi.CreateActiveProductAsync(Client);

        await PublishAsync(Guid.NewGuid(), 0);

        await PublishUntilAsync(sentinel, 0, "OutOfStock");
    }

    /// <summary>
    /// Read from the database, not through the API: the product endpoint caches, and a read that lands between the
    /// status change and the cache invalidation would keep the old status until the entry expires.
    /// </summary>
    private async Task<string> StatusAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var product = await scope.ServiceProvider.GetRequiredService<IProductRepository>().GetByIdAsync(id, default);
        return product!.Status.ToString();
    }

    private async Task PublishAsync(Guid productId, int availableQuantity)
    {
        var connection = Factory.Services.GetRequiredService<IConnection>();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync("inventory-exchange", ExchangeType.Topic, durable: true);

        var body = JsonSerializer.Serialize(new
        {
            ProductId = productId,
            OnHandQuantity = availableQuantity,
            ReservedQuantity = 0,
            AvailableQuantity = availableQuantity
        });

        await channel.BasicPublishAsync("inventory-exchange", "inventory.updated", false, Encoding.UTF8.GetBytes(body));
    }

    /// <summary>
    /// Publishes and waits for the status. The consumer binds its queue after the host starts, so an early message
    /// can be dropped: publish again until it lands.
    /// </summary>
    private async Task PublishUntilAsync(Guid productId, int availableQuantity, string expectedStatus)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await PublishAsync(productId, availableQuantity);

            for (var i = 0; i < 10; i++)
            {
                if (await StatusAsync(productId) == expectedStatus)
                    return;
                await Task.Delay(100);
            }
        }

        (await StatusAsync(productId)).Should().Be(expectedStatus);
    }
}
