# WeatherNews API (.NET 8)

[![Framework](https://img.shields.io/badge/Framework-.NET%208-blue.svg)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Database](https://img.shields.io/badge/Database-SQLite-green.svg)](https://www.sqlite.org/index.html)
[![Test](https://img.shields.io/badge/Testing-xUnit-orange.svg)](https://xunit.net/)

This project is a demonstration of a modern, clean-architecture REST API built with **.NET 8**. It serves as a portfolio piece to showcase my understanding of dependency injection, service-layer patterns, automated testing, and defensive programming in the C# ecosystem.

---

## Purpose of the Project
The goal was to build a robust backend for a news management system that adheres to industry standards. Beyond simple CRUD operations, this project highlights:

* **Separation of Concerns**: Moving business logic out of the `Program.cs` and into a dedicated Service layer.
* **Data Integrity**: Using EF Core with SQLite for persistence and an In-Memory provider for isolated testing.
* **Reliability**: Implementing xUnit test suites to ensure edge cases (like null inputs or missing IDs) are handled gracefully.

---

## Tech Stack
| Component | Technology |
| :--- | :--- |
| **Framework** | .NET 8 (Minimal APIs) |
| **Database** | SQLite (Development) |
| **ORM** | Entity Framework Core |
| **Testing** | xUnit, Microsoft.EntityFrameworkCore.InMemory |
| **Logging** | ILogger with NullLogger implementations |

---

## Key Architectural Decisions

### 1. The Service Pattern
Instead of putting database logic directly in the API endpoints, I implemented a `NewsService`. This makes the code:
* **Testable**: I can test the logic without spinning up a web server.
* **Maintainable**: If I change the database provider later, the API layer remains untouched.

### 2. Defensive Programming & Guard Clauses
I implemented **"Fail-Fast"** logic in the service layer. Using Guard Clauses, the API identifies invalid data or null references immediately, throwing meaningful exceptions rather than allowing the application to crash or save corrupted data.

### 3. Advanced Unit Testing
The testing strategy focuses on **Isolation**.
* Every test generates a unique `Guid` for its In-Memory database name.
* This ensures that no two tests share data, preventing "flaky tests" and ensuring a 100% predictable test environment.


