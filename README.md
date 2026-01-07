## Testability & API Documentation

The API is fully testable via Swagger/OpenAPI.

### Swagger
Swagger UI is available at:
http://localhost:5000/swagger

### Test Data Management

For testing purposes, the API exposes the following endpoints:

#### Seed Data
POST /seed

Recreates the database and populates it with minimal test data:
- Single hotel
- Hotel contains 6 rooms
- No initial bookings

#### Reset Data
POST /reset

Drops and recreates the database, removing all data and preparing the system for reseeding.

### Authentication
The API does not require authentication. All endpoints are accessible without credentials.

## Testing

This project includes **integration tests** to validate end-to-end API behavior.

### Integration Tests
- Implemented using **xUnit**, **FluentAssertions**, and **Microsoft.AspNetCore.Mvc.Testing**
- Tests run against an **in-memory database** for isolation and repeatability
- Covers:
  - Hotel search by keyword
  - Booking creation
  - Prevention of overlapping bookings
  - API availability and startup validation

### Run Tests
```bash
dotnet test
