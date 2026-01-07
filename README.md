## Testability & API Documentation

The API is fully testable via Swagger/OpenAPI.

### Database
The application uses an in-memory database for integration tests, allowing reviewers to run tests without any external database setup or credentials.

No database secrets are required to build or test the application.

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

## Deployment & Hosting

This API is designed to be cloud-ready and containerized.

## Docker & Containerization

The API includes Docker support using a Linux-based container.

A Dockerfile is provided to enable deployment to:
- Azure Container Apps
- Azure App Service (Linux)
- Any Docker-compatible platform

### Local Docker Execution Note
Docker Desktop could not be started locally due to virtualization being unavailable on the current machine.
This is an environment limitation and does not affect the application’s container readiness.

The Dockerfile follows standard .NET 8 container practices and can be built and run successfully in any Docker-enabled environment.

