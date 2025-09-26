# Clean Architecture Implementation

This document describes the Clean Architecture implementation for the Intelligent C# Code Review Bot project.

## 🏗️ Architecture Overview

The project follows Clean Architecture principles with clear separation of concerns across four main layers:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
│                  (Web API Controllers)                      │
└─────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│               (Use Cases, Services, DTOs)                   │
└─────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                      │
│           (External Services, Repositories)                 │
└─────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                           │
│              (Entities, Interfaces, Business Logic)         │
└─────────────────────────────────────────────────────────────┘
```

## 📁 Project Structure

### Source Code (`src/`)

```
src/
├── CodeReviewBot.Domain/           # Core business logic
│   ├── Entities/                   # Domain entities
│   │   ├── CodeIssue.cs
│   │   ├── CodingRule.cs
│   │   ├── FileChange.cs
│   │   └── PullRequest.cs
│   └── Interfaces/                 # Domain interfaces
│       ├── ICodeAnalyzer.cs
│       └── IPullRequestRepository.cs
│
├── CodeReviewBot.Application/      # Application logic
│   ├── DTOs/                       # Data Transfer Objects
│   │   ├── AnalyzePullRequestRequest.cs
│   │   └── AnalyzePullRequestResponse.cs
│   ├── Interfaces/                 # Application interfaces
│   │   └── IPullRequestAnalysisService.cs
│   └── Services/                   # Application services
│       └── PullRequestAnalysisService.cs
│
├── CodeReviewBot.Infrastructure/   # External concerns
│   └── ExternalServices/           # External service implementations
│       ├── AzureDevOpsService.cs
│       └── CodeAnalyzerService.cs
│
└── CodeReviewBot.Presentation/     # Web API layer
    ├── Controllers/                 # API Controllers
    │   └── WebhookController.cs
    ├── Program.cs                   # Application startup
    └── appsettings.json            # Configuration
```

### Test Code (`tests/`)

```
tests/
├── CodeReviewBot.Domain.Tests/     # Domain layer tests
├── CodeReviewBot.Application.Tests/ # Application layer tests
├── CodeReviewBot.Infrastructure.Tests/ # Infrastructure layer tests
└── CodeReviewBot.Presentation.Tests/   # Presentation layer tests
```

## 🎯 Layer Responsibilities

### Domain Layer

- **Purpose**: Contains the core business logic and entities
- **Dependencies**: None (pure C# classes)
- **Contains**:
  - Business entities (`CodeIssue`, `CodingRule`, `FileChange`, `PullRequest`)
  - Domain interfaces (`ICodeAnalyzer`, `IPullRequestRepository`)
  - Business rules and validation

### Application Layer

- **Purpose**: Orchestrates the application flow and use cases
- **Dependencies**: Domain layer only
- **Contains**:
  - Use case services (`PullRequestAnalysisService`)
  - DTOs for data transfer
  - Application interfaces
  - Business logic orchestration

### Infrastructure Layer

- **Purpose**: Implements external concerns and data access
- **Dependencies**: Domain and Application layers
- **Contains**:
  - Azure DevOps API integration (`AzureDevOpsService`)
  - Code analysis implementation (`CodeAnalyzerService`)
  - External service implementations
  - Data persistence logic

### Presentation Layer

- **Purpose**: Handles web requests and responses
- **Dependencies**: All other layers
- **Contains**:
  - Web API controllers
  - Request/response handling
  - Authentication and authorization
  - Application startup and configuration

## 🔄 Dependency Flow

The dependency flow follows Clean Architecture principles:

1. **Domain** ← **Application** ← **Infrastructure** ← **Presentation**
2. **Inner layers** don't know about **outer layers**
3. **Dependencies point inward** toward the domain
4. **Interface segregation** at domain boundaries

## 🧪 Testing Strategy

Each layer has its corresponding test project:

- **Domain Tests**: Unit tests for business logic and entities
- **Application Tests**: Unit tests for use cases and services (with mocked dependencies)
- **Infrastructure Tests**: Integration tests for external services
- **Presentation Tests**: API integration tests

## 📦 Project Dependencies

### Domain Layer

```xml
<PackageReference Include="FluentValidation" Version="11.8.1" />
```

### Application Layer

```xml
<PackageReference Include="MediatR" Version="12.2.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
```

### Infrastructure Layer

```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Polly" Version="8.2.0" />
```

### Presentation Layer

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
```

## 🚀 Benefits of This Architecture

1. **Separation of Concerns**: Each layer has a single responsibility
2. **Testability**: Easy to unit test with proper mocking
3. **Maintainability**: Changes in one layer don't affect others
4. **Flexibility**: Easy to swap implementations (e.g., different code analyzers)
5. **Scalability**: Can add new features without affecting existing code
6. **Independence**: Domain logic is independent of external frameworks

## 🔧 Building and Running

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run the application
dotnet run --project src/CodeReviewBot.Presentation

# Run specific test project
dotnet test tests/CodeReviewBot.Application.Tests
```

## 📋 Key Features

- ✅ **Clean Architecture** implementation
- ✅ **Dependency Injection** throughout all layers
- ✅ **Comprehensive testing** with unit and integration tests
- ✅ **FluentValidation** for input validation
- ✅ **Serilog** for structured logging
- ✅ **Swagger/OpenAPI** for API documentation
- ✅ **Resilience patterns** with Polly
- ✅ **Configuration management** with appsettings.json

This Clean Architecture implementation provides a solid foundation for the Code Review Bot, making it maintainable, testable, and scalable for future enhancements.
