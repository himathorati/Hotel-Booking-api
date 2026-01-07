using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class BookingIntegrationTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BookingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task ResetAndSeedAsync()
    {
        await _client.PostAsync("/reset", null);
        await _client.PostAsync("/seed", null);
    }

    [Fact]
    public async Task Can_seed_database()
    {
        var response = await _client.PostAsync("/seed", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Can_search_hotels_by_keyword()
    {
        await ResetAndSeedAsync();

        var response = await _client.GetAsync("/hotels/search?HotelName=River");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("River");
    }

    [Fact]
    public async Task Can_book_a_room_successfully()
    {
        await ResetAndSeedAsync();

        var bookingRequest = new
        {
            hotelId = 1,
            people = 2,
            from = "2026-01-10T10:00:00",
            to = "2026-01-12T10:00:00"
        };

        var response = await _client.PostAsJsonAsync("/book", bookingRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("bookingReference");
    }

    [Fact]
    public async Task Cannot_book_overlapping_dates_for_same_room()
    {
        await ResetAndSeedAsync();

        var firstBooking = new
        {
            hotelId = 1,
            people = 2,
            from = "2026-01-10T10:00:00",
            to = "2026-01-12T10:00:00"
        };

        var secondBooking = new
        {
            hotelId = 1,
            people = 2,
            from = "2026-01-11T09:00:00", // overlaps
            to = "2026-01-13T10:00:00"
        };

        var firstResponse = await _client.PostAsJsonAsync("/book", firstBooking);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await _client.PostAsJsonAsync("/book", secondBooking);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var message = await secondResponse.Content.ReadAsStringAsync();
        message.Should().Contain("already booked");
    }

    [Fact]
    public async Task Booking_reference_should_be_unique()
    {
        await ResetAndSeedAsync();

        var booking1 = new
        {
            hotelId = 1,
            people = 1,
            from = "2026-01-01T10:00:00",
            to = "2026-01-02T10:00:00"
        };

        var booking2 = new
        {
            hotelId = 1,
            people = 1,
            from = "2026-01-03T10:00:00",
            to = "2026-01-04T10:00:00"
        };

        var r1 = await _client.PostAsJsonAsync("/book", booking1);
        var r2 = await _client.PostAsJsonAsync("/book", booking2);

        var ref1 = await r1.Content.ReadAsStringAsync();
        var ref2 = await r2.Content.ReadAsStringAsync();

        ref1.Should().NotBe(ref2);
    }
}
