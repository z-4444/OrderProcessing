# Order Processing Application

## Overview
The **Order Processing Application** is a simple ASP.NET Core MVC web app designed to manage orders efficiently. It allows users to input an order amount and type (e.g., Standard or Express), calculates a total with fees, and displays the result. Built as an assignment, it showcases clean code, styling with Bootstrap, input validation, automated testing with xUnit, and continuous integration with GitHub Actions.

### Features
- **Order Entry**: Input order amount and type via a styled form (`Order/Index`).
- **Result Display**: View calculated order totals on a separate page (`Order/Result`).
- **UI**: Responsive design with Bootstrap, a sticky footer, and custom CSS.
- **Validation**: Client-side and server-side validation for order inputs.
- **Testing**: Automated unit tests for the `OrderController`.
- **CI/CD**: GitHub Actions pipeline to build and test on every push/PR.

### Project Structure
```
OrderProcessing/
├── src/
│   ├── OrderProcessing.Api/             # ASP.NET Core Web API (primary host)
│   ├── OrderProcessing.Application/     # Use cases, DTOs, validation
│   ├── OrderProcessing.Domain/          # Domain model
│   └── OrderProcessing.Infrastructure/  # EF Core + Identity + SQL Server
├── tests/
│   ├── OrderProcessing.UnitTests/
│   └── OrderProcessing.IntegrationTests/ # SQL Server / Testcontainers tests
├── OrderProcessing/                       # Temporary MVC host (legacy)
├── OrderProcessing.Tests/                 # Legacy MVC controller tests
├── docker-compose.yml                     # Local SQL Server
├── .env.example
├── OrderProcessing.sln
└── README.md
```

> **Note:** The Web API under `src/OrderProcessing.Api` is now the primary host. The MVC project remains temporarily for compatibility and will be removed after Angular is introduced.

## Setup Instructions

### Prerequisites
- **.NET 8.0 SDK**: [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Visual Studio 2022**: [Download here](https://visualstudio.microsoft.com/vs/) (optional, for IDE support)
- **Git**: [Download here](https://git-scm.com/downloads)

### Clone the Repository
1. Open a terminal (e.g., Git Bash).
2. Clone the repo:
   ```bash
   git clone https://github.com/z-4444/OrderProcessing.git
   cd OrderProcessing

Build the Solution
Restore dependencies:
```bash
dotnet restore OrderProcessing.sln

Build the projects:
```
dotnet build OrderProcessing.sln --configuration Release
```

Run the Application
Navigate to the main project:
```bash
cd OrderProcessing
```

Run the app:
```bash
dotnet run
```

Open a browser and visit https://localhost:5001/Order/Index (port may vary—check console output).

### Local SQL Server (Docker)

SQL Server password must include uppercase, lowercase, a number, and a symbol.

```bash
cp .env.example .env
docker compose up -d
docker compose ps
```

Apply migrations (does not run automatically on app startup):

```bash
dotnet ef database update --project src/OrderProcessing.Infrastructure --startup-project src/OrderProcessing.Api
```

### Run the API

Copy `src/OrderProcessing.Api/appsettings.Development.example.json` to `appsettings.Development.json` (already present locally) and set:

- `Jwt:Key` — at least 32 characters
- `SeedAdmin:Password` — Identity-compliant password (do not commit production secrets)

Then:

```bash
dotnet run --project src/OrderProcessing.Api
```

Open Swagger at `/swagger`. Login with `POST /api/auth/login`, click **Authorize**, and paste `Bearer {token}`.

Refresh tokens are intentionally not implemented in this milestone; access tokens are short-lived JWTs.

The legacy MVC project remains in the solution but is no longer the primary host.

Integration tests require Docker and start an isolated SQL Server container via Testcontainers.

### Run Tests
From the root directory:
```
dotnet test OrderProcessing.sln
```

Or in Visual Studio:
Open OrderProcessingSolution.sln.
Go to Test > Test Explorer > Run All Tests.

CI/CD
The project uses GitHub Actions for CI. On every push or pull request to main, development, or feature/* branches:
Builds the solution (OrderProcessingSolution.sln).
Runs all xUnit tests.
Check workflow runs at GitHub Actions.

### Usage
Place an Order:
Go to /Order/Index.
Enter an order amount (1–10,000) and select a type (New or Loyal).
Click “Calculate & View Result”.
View Result:
See the order summary with calculated total (e.g., 10% for Loyal).
Explore:
Visit /Home/Index for a welcome page or /Home/Privacy for a privacy note.

### Contributing
This is an assignment project by Muhammad Zahid (z-4444). Feel free to fork and submit pull requests for enhancements!

### License
For educational purposes only. No formal license applied.
