using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("HotelDb")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // or Migrate() if migrations exist
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/seed", async (AppDbContext db) =>
{
    // 1️⃣ Delete dependent entities FIRST
    db.Bookings.RemoveRange(db.Bookings);
    await db.SaveChangesAsync();

    db.Rooms.RemoveRange(db.Rooms);
    await db.SaveChangesAsync();

    db.Hotels.RemoveRange(db.Hotels);
    await db.SaveChangesAsync();    

    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    // 2️⃣ Seed fresh data
    var hotel = new Hotel
    {
        Name = "River View Retreat",
        Rooms = new List<Room>
        {
            new() { RoomType = RoomType.Single, Capacity = 1 },
            new() { RoomType = RoomType.Single, Capacity = 1 },
            new() { RoomType = RoomType.Double, Capacity = 2 },
            new() { RoomType = RoomType.Double, Capacity = 2 },
            new() { RoomType = RoomType.Deluxe, Capacity = 4 },
            new() { RoomType = RoomType.Deluxe, Capacity = 4 }
        }        
    };

    db.Hotels.Add(hotel);
    await db.SaveChangesAsync();

    return Results.Ok("Database reset and seeded successfully");
});


app.MapPost("/reset", async (AppDbContext db) =>
{
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    return Results.Ok("Database dropped and recreated");
 
});

app.MapGet("/hotels/search", async (string HotelName, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(HotelName))
    return Results.Ok("Search query cannot be empty.");

    HotelName = HotelName.ToLower();

    var hotels = await db.Hotels
        .Where(h => h.Name.ToLower().Contains(HotelName))
        .Select(h => new
        {
            h.Id,
            h.Name
        })
        .ToListAsync();

    return Results.Ok(hotels);
});


app.MapGet("/availability", async (
    string hotelName,
    DateTime from,
    DateTime to,
    int people,
    AppDbContext db) =>
{
    var hotel = await db.Hotels
        .Include(h => h.Rooms)
        .Include(h => h.Bookings)
        .ThenInclude(b => b.Room)
        .FirstOrDefaultAsync(h => h.Name == hotelName);

    if (hotel == null) return Results.NotFound("Hotel not found");

    var availableRooms = hotel.Rooms
        .Where(r => r.Capacity >= people)
        .Where(r => !hotel.Bookings.Any(b =>
            b.RoomId == r.Id &&
            from < b.To &&
            to > b.From))
        .ToList();

    return Results.Ok(availableRooms);
})
    .WithOpenApi(operation =>
    {
        operation.Summary = "Find available rooms";
        operation.Description =
            "Search available rooms for a hotel within a date range.\n\n" +
            "**Date format:** `yyyy-MM-dd` or `yyyy-MM-dd`\n\n" +
            "Example: `2026-01-21` or `2026-01-25T00:00:00`";

        operation.Parameters.First(p => p.Name == "from").Description =
            "Start date (format: yyyy-MM-dd or yyyy-MM-dd)";

        operation.Parameters.First(p => p.Name == "to").Description =
            "End date (format: yyyy-MM-dd or yyyy-MM-dd)";

        return operation;
    });

app.MapPost("/book", async (BookingRequest request, AppDbContext db) =>
{
    var hotel = await db.Hotels
        .Include(h => h.Rooms)
        .FirstOrDefaultAsync(h => h.Id == request.HotelId);

    if (hotel == null)
        return Results.NotFound("Hotel not found");

    var room = hotel.Rooms
        .OrderBy(r => r.Id)
        .FirstOrDefault(r => r.Capacity >= request.People);

    if (room == null)
        return Results.BadRequest("No suitable room");

    // ❗ Check overlap ONLY for THIS room
    var overlapExists = await db.Bookings.AnyAsync(b =>
        b.RoomId == room.Id &&
        request.From < b.To &&
        request.To > b.From
    );

    if (overlapExists)
        return Results.BadRequest("Room already booked for selected dates");

    var booking = new Booking
    {
        BookingReference = Guid.NewGuid().ToString("N"),
        HotelId = hotel.Id,
        RoomId = room.Id,
        From = request.From,
        To = request.To,
        People = request.People
    };

    db.Bookings.Add(booking);
    await db.SaveChangesAsync();

    return Results.Ok(new { booking.BookingReference });
})
.WithOpenApi(operation =>
{
    operation.Summary = "Book a room";
    operation.Description =
        "Creates a room booking for the given hotel and date range.\n\n" +
        "**Date format:** `yyyy-MM-dd` or `yyyy-MM-ddTHH:mm:ss`\n\n" +
        "Example:\n" +
        "`2026-03-01` or `2026-01-21T00:00:00`";

    // Example request body
    operation.RequestBody.Content["application/json"].Example =
        new Microsoft.OpenApi.Any.OpenApiObject
        {
            ["hotelId"] = new Microsoft.OpenApi.Any.OpenApiInteger(1),
            ["from"] = new Microsoft.OpenApi.Any.OpenApiString("2026-01-21"),
            ["to"] = new Microsoft.OpenApi.Any.OpenApiString("2026-01-25"),
            ["people"] = new Microsoft.OpenApi.Any.OpenApiInteger(2)
        };

    return operation;
}); ;

app.MapGet("/booking/{reference}", async (string reference, AppDbContext db) =>
{
    var booking = await db.Bookings
        .Include(b => b.Room)
        .Include(b => b.Hotel)
        .FirstOrDefaultAsync(b => b.BookingReference == reference);

    return booking is null ? Results.NotFound() : Results.Ok(new BookingResponse
    {
        BookingReference = booking.BookingReference,
        From = booking.From,
        To = booking.To,
        People = booking.People,
        HotelName = booking.Hotel!.Name,
        RoomType = booking.Room!.RoomType,
        RoomCapacity = booking.Room.Capacity
    });
});

app.Run();

public partial class Program { }

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options) : base(options) { }
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>()
            .Property(r => r.RoomType)
            .HasConversion<string>();
    }
}

public class Hotel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Room> Rooms { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}

public class Room
{
    public int Id { get; set; }
    public RoomType RoomType { get; set; }
    public int Capacity { get; set; }
}

public class Booking
{
    public int Id { get; set; }
    public string BookingReference { get; set; } = "";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int People { get; set; }

    public int RoomId { get; set; }
    public int HotelId { get; set; }

    public Room? Room { get; set; }
    public Hotel? Hotel { get; set; }
}

public class BookingRequest
{
    [Required] public int HotelId { get; set; }
    [Required] public DateTime From { get; set; }
    [Required] public DateTime To { get; set; }
    [Required] public int People { get; set; }
}

public class BookingResponse
{
    public string BookingReference { get; set; } = "";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int People { get; set; }

    public string HotelName { get; set; } = "";
    public RoomType RoomType { get; set; }
    public int RoomCapacity { get; set; }
}


public enum RoomType
{
    Single,
    Double,
    Deluxe
}
