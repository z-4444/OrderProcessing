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
│   ├── OrderProcessing.Domain/          # Domain model
│   └── OrderProcessing.Infrastructure/  # EF Core + SQL Server persistence
├── tests/
│   ├── OrderProcessing.UnitTests/
│   └── OrderProcessing.IntegrationTests/ # SQL Server / Testcontainers tests
├── OrderProcessing/                       # Temporary MVC host
├── OrderProcessing.Tests/                 # Legacy MVC controller tests
├── docker-compose.yml                     # Local SQL Server
├── .env.example
├── OrderProcessing.sln
└── README.md
```

> **Note:** The repository is being migrated incrementally. The MVC project under `OrderProcessing/` remains the temporary runnable host while the new domain layer is introduced under `src/`. The final API and Angular frontend will be added in later milestones.

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
dotnet ef database update --project src/OrderProcessing.Infrastructure --startup-project src/OrderProcessing.Infrastructure
```

Optional development seed (off by default): set `Database:SeedOnStartup` to `true` in `appsettings.Development.json`, then `dotnet run --project OrderProcessing`.

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
