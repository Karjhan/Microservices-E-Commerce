using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using RabbitMQ.Client;
using Shared.Security.TestSupport;

namespace IntegrationTests.Flows;

public class AdminStockAdjustmentFlowTests : IntegrationTestBase
{
    private static readonly Guid AdminId = Guid.NewGuid();

    public AdminStockAdjustmentFlowTests(InventoryApiFactory factory) : base(factory) { }

    private HttpClient Admin() => Factory.CreateClient().AsAdmin(AdminId);

    private Task<HttpResponseMessage> Adjust(Guid productId, string mode, int quantity, string reason, string? expectedVersion = null) =>
        Client.PostAsJsonAsync($"/inventory/{productId}/adjustments", new { mode, quantity, reason, expectedVersion });

    private async Task<InventoryItemResponse> Stock(Guid productId)
    {
        var response = await Client.GetAsync($"/inventory/{productId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
    }

    private async Task<Guid> SeedAsync(int onHand, string reason = "Initial stock")
    {
        var productId = Guid.NewGuid();
        (await Adjust(productId, "set", onHand, reason)).EnsureSuccessStatusCode();
        return productId;
    }

    [Fact]
    public async Task Stock_of_5_with_an_order_for_2_shows_3_available_and_2_reserved_then_plus_10_updates_everything()
    {
        var productId = await SeedAsync(5);

        await PublishOrderCreated(Guid.NewGuid(), productId, quantity: 2);
        await WaitForAsync(productId, s => s.ReservedQuantity == 2);

        var afterOrder = await Stock(productId);
        afterOrder.OnHandQuantity.Should().Be(5);
        afterOrder.ReservedQuantity.Should().Be(2);
        afterOrder.AvailableQuantity.Should().Be(3);

        (await Adjust(productId, "delta", 10, "Supplier delivery")).EnsureSuccessStatusCode();

        var afterDelivery = await Stock(productId);
        afterDelivery.OnHandQuantity.Should().Be(15);
        afterDelivery.ReservedQuantity.Should().Be(2);
        afterDelivery.AvailableQuantity.Should().Be(13);

        var history = await Movements(productId);
        history.Should().Contain(m => m.GetProperty("reason").GetString() == "Supplier delivery"
                                      && m.GetProperty("delta").GetInt32() == 10
                                      && m.GetProperty("resultingOnHand").GetInt32() == 15);
    }

    [Fact]
    public async Task Set_replaces_the_count_and_the_history_shows_the_difference()
    {
        var productId = await SeedAsync(10);

        var response = await Adjust(productId, "set", 4, "Recount after inventory");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Stock(productId)).OnHandQuantity.Should().Be(4);
        (await Movements(productId)).Should().Contain(m =>
            m.GetProperty("delta").GetInt32() == -6 && m.GetProperty("resultingOnHand").GetInt32() == 4);
    }

    [Fact]
    public async Task Mode_is_required_and_must_be_delta_or_set()
    {
        var productId = await SeedAsync(10);

        var response = await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments", new { mode = "multiply", quantity = 2, reason = "should be refused" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Problem(response)).GetProperty("errors").GetProperty("mode").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_change_below_the_reserved_count_is_refused_with_422_and_changes_nothing()
    {
        var productId = await SeedAsync(10);
        await PublishOrderCreated(Guid.NewGuid(), productId, quantity: 6);
        await WaitForAsync(productId, s => s.ReservedQuantity == 6);
        var before = await Stock(productId);

        var response = await Adjust(productId, "delta", -5, "Should not be possible");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Problem(response)).GetProperty("code").GetString().Should().Be("below_reserved");
        (await Stock(productId)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task A_change_below_zero_is_refused_with_422()
    {
        var productId = await SeedAsync(3);

        var response = await Adjust(productId, "delta", -4, "Should not be possible");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Problem(response)).GetProperty("code").GetString().Should().Be("below_zero");
        (await Stock(productId)).OnHandQuantity.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task A_reason_under_3_characters_is_a_400_naming_the_field(string reason)
    {
        var productId = await SeedAsync(3);

        var response = await Adjust(productId, "delta", 1, reason);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Problem(response)).GetProperty("errors").GetProperty("reason").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_reason_over_200_characters_is_a_400()
    {
        var productId = await SeedAsync(3);

        var response = await Adjust(productId, "delta", 1, new string('x', 201));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_records_actor_delta_reason_and_resulting_on_hand_newest_first()
    {
        var productId = await SeedAsync(10, "Opening count");
        (await Adjust(productId, "delta", -3, "Damaged in transit")).EnsureSuccessStatusCode();
        (await Adjust(productId, "set", 20, "Recount")).EnsureSuccessStatusCode();

        var history = await Movements(productId);

        history.Should().HaveCount(3);
        history.Select(m => m.GetProperty("reason").GetString())
            .Should().ContainInOrder("Recount", "Damaged in transit", "Opening count");
        history.Select(m => m.GetProperty("resultingOnHand").GetInt32()).Should().ContainInOrder(20, 7, 10);
        history.Select(m => m.GetProperty("delta").GetInt32()).Should().ContainInOrder(13, -3, 10);
        history.Should().OnlyContain(m => m.GetProperty("actorId").GetString() == Guid.Empty.ToString()
                                          || m.GetProperty("actorId").ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task History_names_the_admin_who_made_the_change()
    {
        var productId = Guid.NewGuid();
        using var admin = Admin();

        (await admin.PostAsJsonAsync($"/inventory/{productId}/adjustments",
            new { mode = "set", quantity = 8, reason = "Opening count" })).EnsureSuccessStatusCode();

        var history = await Movements(productId);

        history.Should().ContainSingle()
            .Which.GetProperty("actorId").GetString().Should().Be(AdminId.ToString());
    }

    [Fact]
    public async Task History_is_paged()
    {
        var productId = await SeedAsync(1, "Opening count");
        for (var i = 0; i < 4; i++)
            (await Adjust(productId, "delta", 1, $"Delivery {i}")).EnsureSuccessStatusCode();

        var page = await Client.GetFromJsonAsync<JsonElement>($"/inventory/{productId}/movements?page=2&pageSize=2");

        page.GetProperty("totalCount").GetInt32().Should().Be(5);
        page.GetProperty("totalPages").GetInt32().Should().Be(3);
        page.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task History_of_a_product_without_stock_is_404()
    {
        (await Client.GetAsync($"/inventory/{Guid.NewGuid()}/movements")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_change_based_on_an_old_version_is_a_409_stale_version_and_the_newer_change_stays()
    {
        var productId = await SeedAsync(10);
        var seenByBothAdmins = (await Stock(productId)).Version;

        (await Adjust(productId, "set", 12, "First admin recount", seenByBothAdmins)).EnsureSuccessStatusCode();

        var second = await Adjust(productId, "set", 99, "Second admin recount", seenByBothAdmins);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Problem(second)).GetProperty("code").GetString().Should().Be("stale_version");
        (await Stock(productId)).OnHandQuantity.Should().Be(12);
    }

    [Fact]
    public async Task Concurrent_adjustments_from_the_same_version_let_one_win_and_return_409_to_the_rest()
    {
        var productId = await SeedAsync(10);
        var version = (await Stock(productId)).Version;

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            Adjust(productId, "delta", 1, $"Concurrent change {i}", version)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK)
            .Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
        (await Stock(productId)).OnHandQuantity.Should().Be(11);
    }

    [Fact]
    public async Task Concurrent_changes_without_a_version_are_never_silently_lost()
    {
        var productId = await SeedAsync(10);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            Adjust(productId, "delta", 1, $"Concurrent change {i}")));

        // Each response is either applied or told to retry; the count matches exactly what was applied.
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        var applied = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        (await Stock(productId)).OnHandQuantity.Should().Be(10 + applied);
        (await Movements(productId)).Should().HaveCount(1 + applied);
    }

    [Fact]
    public async Task The_threshold_defaults_to_5_and_can_be_changed_per_product()
    {
        var productId = await SeedAsync(8);
        (await Stock(productId)).LowStockThreshold.Should().Be(5);
        (await Stock(productId)).Status.Should().Be("ok");

        var response = await Client.PutAsJsonAsync($"/inventory/{productId}/threshold", new { lowStockThreshold = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await Stock(productId);
        after.LowStockThreshold.Should().Be(10);
        after.Status.Should().Be("low");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public async Task A_threshold_out_of_range_is_a_400(int threshold)
    {
        var productId = await SeedAsync(8);

        var response = await Client.PutAsJsonAsync($"/inventory/{productId}/threshold", new { lowStockThreshold = threshold });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Stock(productId)).LowStockThreshold.Should().Be(5);
    }

    [Fact]
    public async Task The_list_is_sorted_by_urgency_and_filters_by_low_and_out()
    {
        var ok = await SeedAsync(50);
        var low = await SeedAsync(2);
        var out1 = await SeedAsync(0);
        var ids = string.Join(',', ok, low, out1);

        var all = await ListAsync($"productIds={ids}");
        all.Select(i => i.GetProperty("productId").GetGuid()).Should().ContainInOrder(out1, low, ok);

        (await ListAsync($"productIds={ids}&status=low")).Select(i => i.GetProperty("productId").GetGuid())
            .Should().Equal(low);
        (await ListAsync($"productIds={ids}&status=out")).Select(i => i.GetProperty("productId").GetGuid())
            .Should().Equal(out1);
        (await ListAsync($"productIds={ids}&status=all")).Should().HaveCount(3);
    }

    [Fact]
    public async Task The_list_reports_the_threshold_and_follows_a_changed_one()
    {
        var productId = await SeedAsync(20);
        var ids = productId.ToString();

        (await ListAsync($"productIds={ids}&status=low")).Should().BeEmpty();

        (await Client.PutAsJsonAsync($"/inventory/{productId}/threshold", new { lowStockThreshold = 25 }))
            .EnsureSuccessStatusCode();

        var low = await ListAsync($"productIds={ids}&status=low");
        low.Should().ContainSingle().Which.GetProperty("lowStockThreshold").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task An_unknown_status_is_a_400()
    {
        (await Client.GetAsync("/inventory?status=nearly")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reserved_units_count_against_availability_in_the_filters()
    {
        var productId = await SeedAsync(6);
        await PublishOrderCreated(Guid.NewGuid(), productId, quantity: 6);
        await WaitForAsync(productId, s => s.ReservedQuantity == 6);

        (await ListAsync($"productIds={productId}&status=out")).Should().ContainSingle();
    }

    [Fact]
    public async Task A_shopper_reads_only_availability_and_an_admin_reads_the_full_record()
    {
        var productId = await SeedAsync(10);
        await PublishOrderCreated(Guid.NewGuid(), productId, quantity: 4);
        await WaitForAsync(productId, s => s.ReservedQuantity == 4);

        using var anonymous = Factory.CreateClient();
        var shopper = await anonymous.GetFromJsonAsync<JsonElement>($"/inventory/{productId}");

        shopper.GetProperty("available").GetInt32().Should().Be(6);
        shopper.GetProperty("inStock").GetBoolean().Should().BeTrue();
        shopper.TryGetProperty("onHandQuantity", out _).Should().BeFalse();
        shopper.TryGetProperty("reservedQuantity", out _).Should().BeFalse();
        shopper.TryGetProperty("lowStockThreshold", out _).Should().BeFalse();

        var admin = await Client.GetFromJsonAsync<JsonElement>($"/inventory/{productId}");
        admin.GetProperty("onHandQuantity").GetInt32().Should().Be(10);
        admin.GetProperty("reservedQuantity").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task A_customer_token_gets_the_shopper_view_too()
    {
        var productId = await SeedAsync(10);
        using var customer = Factory.CreateClient().AsCustomer();

        var view = await customer.GetFromJsonAsync<JsonElement>($"/inventory/{productId}");

        view.TryGetProperty("onHandQuantity", out _).Should().BeFalse();
        view.GetProperty("available").GetInt32().Should().Be(10);
    }


    private static async Task<JsonElement> Problem(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<List<JsonElement>> Movements(Guid productId)
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/inventory/{productId}/movements?pageSize=100");
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    private async Task<List<JsonElement>> ListAsync(string query)
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/inventory?{query}&pageSize=100");
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    private async Task WaitForAsync(Guid productId, Func<InventoryItemResponse, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var dto = await Stock(productId);
            if (predicate(dto))
                return;

            await Task.Delay(250);
        }

        Assert.Fail($"Inventory item {productId} did not reach the expected state within the timeout.");
    }

    private async Task PublishOrderCreated(Guid orderId, Guid productId, int quantity)
    {
        var factory = new ConnectionFactory
        {
            HostName = Factory.RabbitHost,
            Port = Factory.RabbitPort,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync("orders-exchange", ExchangeType.Topic, durable: true);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            orderId,
            userId = Guid.NewGuid(),
            totalAmount = 100m,
            currency = "EUR",
            itemCount = 1,
            createdAt = DateTime.UtcNow,
            items = new[] { new { productId, quantity } }
        }));

        await channel.BasicPublishAsync("orders-exchange", "order.created", mandatory: false, body: body);
    }
}
