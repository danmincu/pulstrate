# Task Server Backend - Technical Requirements Document

## Project Overview

Build a **ASP.NET Core 10** backend server for task management and execution, deployable to **K3s (Kubernetes)**. The server provides a task execution framework with real-time progress tracking via SignalR and RESTful CRUD operations.

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET Core 10 (.NET 10) |
| Real-time Communication | SignalR |
| API Documentation | Swagger/OpenAPI |
| Authentication | JWT (Firebase tokens) |
| Container Orchestration | K3s (Kubernetes) |
| Initial Storage | In-Memory (IoC pattern for future replacement) |

---

## 1. Task Domain Model

### 1.1 Task Entity

```csharp
public class TaskItem
{
    public Guid Id { get; set; }                    // UUID - auto-generated or client-provided
    public Guid OwnerId { get; set; }               // Owner UUID from JWT claims
    public int Priority { get; set; }               // Integer priority (higher = more important)
    public string Type { get; set; }                // Task type identifier (maps to executor)
    public string Payload { get; set; }             // JSON payload with task-specific details
    public TaskState State { get; set; }            // Current execution state
    public double Progress { get; set; }            // 0-100 percentage
    public string? ProgressDetails { get; set; }    // Human-readable progress info
    public string? StateDetails { get; set; }       // Details about current state (e.g., error message)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

### 1.2 Task States

```csharp
public enum TaskState
{
    Queued,       // Task created, waiting to execute
    Executing,    // Currently running
    Cancelled,    // Cancelled by user request
    Errored,      // Failed with error
    Completed,    // Successfully finished
    Terminated    // Forcefully stopped by system
}
```

### 1.3 Task Executor Interface

```csharp
public interface ITaskExecutor
{
    string TaskType { get; }  // The type string this executor handles
    
    Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken
    );
}

public record TaskProgressUpdate(
    double Percentage,        // 0-100
    string? Details = null
);

public record TaskStateChange(
    Guid TaskId,
    TaskState NewState,
    string? Details = null
);
```

---

## 2. REST API Specification

### 2.1 API Versioning

- Use **URL path versioning**: `/api/v1/tasks`
- Initial version: `1.0`
- Configure via `Asp.Versioning.Mvc` package

### 2.2 Endpoints

#### Tasks Controller (`/api/v1/tasks`)

| Method | Endpoint | Description | Request Body | Response |
|--------|----------|-------------|--------------|----------|
| `GET` | `/api/v1/tasks` | List all tasks for authenticated user | - | `TaskItem[]` |
| `GET` | `/api/v1/tasks/{id}` | Get task by ID | - | `TaskItem` |
| `POST` | `/api/v1/tasks` | Create new task | `CreateTaskRequest` | `TaskItem` |
| `PUT` | `/api/v1/tasks/{id}` | Update task (only if Queued) | `UpdateTaskRequest` | `TaskItem` |
| `DELETE` | `/api/v1/tasks/{id}` | Delete/Cancel task | - | `204 No Content` |
| `POST` | `/api/v1/tasks/{id}/cancel` | Request task cancellation | - | `TaskItem` |

#### Request/Response DTOs

```csharp
public record CreateTaskRequest(
    Guid? Id,              // Optional client-generated UUID
    int Priority,
    string Type,
    string Payload         // JSON string
);

public record UpdateTaskRequest(
    int? Priority,
    string? Payload
);

public record TaskResponse(
    Guid Id,
    Guid OwnerId,
    int Priority,
    string Type,
    string Payload,
    string State,
    double Progress,
    string? ProgressDetails,
    string? StateDetails,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt
);
```

### 2.3 Swagger Configuration

- Enable Swagger UI at `/swagger`
- Include JWT authentication in Swagger
- Document all endpoints with XML comments
- Group by API version

---

## 3. SignalR Hub Specification

### 3.1 Hub Endpoint

- Hub URL: `/hubs/tasks`
- Requires JWT authentication (same as REST API)

### 3.2 Client-to-Server Methods (Hub Methods)

```csharp
public interface ITaskHub
{
    // CRUD Operations
    Task<TaskResponse> CreateTask(CreateTaskRequest request);
    Task<TaskResponse> GetTask(Guid id);
    Task<TaskResponse[]> GetTasks();
    Task<TaskResponse> UpdateTask(Guid id, UpdateTaskRequest request);
    Task DeleteTask(Guid id);
    Task<TaskResponse> CancelTask(Guid id);
    
    // Subscriptions
    Task SubscribeToTask(Guid taskId);
    Task UnsubscribeFromTask(Guid taskId);
    Task SubscribeToAllUserTasks();
    Task UnsubscribeFromAllUserTasks();
}
```

### 3.3 Server-to-Client Methods (Client Callbacks)

```csharp
public interface ITaskHubClient
{
    // Real-time events
    Task OnTaskCreated(TaskResponse task);
    Task OnTaskUpdated(TaskResponse task);
    Task OnTaskDeleted(Guid taskId);
    Task OnStateChanged(Guid taskId, string newState, string? details);
    Task OnProgress(Guid taskId, double percentage, string? details);
}
```

### 3.4 SignalR Groups

- Each user has a group: `user:{userId}`
- Each task has a group: `task:{taskId}`
- Clients can subscribe/unsubscribe to specific tasks

---

## 4. Authentication & Authorization

### 4.1 JWT Token Validation

**Token Structure (Firebase ID Token)**:
```json
{
  "name": "User Name",
  "picture": "https://...",
  "iss": "https://securetoken.google.com/intelpro-23055",
  "aud": "intelpro-23055",
  "auth_time": 1767662564,
  "user_id": "vKvBa8UJMMS2aLX6ZP2ORo5rzO53",
  "sub": "vKvBa8UJMMS2aLX6ZP2ORo5rzO53",
  "iat": 1767924578,
  "exp": 1767928178,
  "email": "user@example.com",
  "email_verified": true,
  "firebase": {
    "identities": { ... },
    "sign_in_provider": "password"
  }
}
```

**Validation Requirements**:
- Validate `aud` claim equals `"intelpro-23055"` (configurable)
- Validate `iss` claim equals `"https://securetoken.google.com/intelpro-23055"` (configurable)
- Validate `exp` (expiration) - reject expired tokens
- Validate `iat` (issued at) - reject tokens issued in the future
- Validate signature using Firebase public keys (JWKS endpoint)

**Firebase JWKS Endpoint**:
```
https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com
```

### 4.2 User Identity Extraction

Extract from JWT claims:
- `sub` or `user_id` → User's unique identifier (use as `OwnerId`)
- `email` → User's email
- `name` → User's display name

### 4.3 Authorization Policies

```csharp
// Define authorization policies
services.AddAuthorization(options =>
{
    // Default policy - just requires authentication
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    
    // Task owner policy - user can only access their own tasks
    options.AddPolicy("TaskOwner", policy =>
        policy.Requirements.Add(new TaskOwnerRequirement()));
    
    // Future: Admin policy
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("admin", "true"));
});
```

### 4.4 Configuration

```json
{
  "Authentication": {
    "Firebase": {
      "ProjectId": "intelpro-23055",
      "ValidAudience": "intelpro-23055",
      "ValidIssuer": "https://securetoken.google.com/intelpro-23055"
    }
  }
}
```

---

## 5. Task Execution Framework

### 5.1 Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    Task Server                          │
├─────────────────────────────────────────────────────────┤
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐ │
│  │  REST API   │    │  SignalR    │    │   Swagger   │ │
│  │  Controller │    │    Hub      │    │     UI      │ │
│  └──────┬──────┘    └──────┬──────┘    └─────────────┘ │
│         │                  │                            │
│         └────────┬─────────┘                            │
│                  ▼                                      │
│         ┌───────────────┐                               │
│         │ ITaskService  │ ◄── Business Logic            │
│         └───────┬───────┘                               │
│                 │                                       │
│    ┌────────────┼────────────┐                          │
│    ▼            ▼            ▼                          │
│ ┌──────┐  ┌──────────┐  ┌──────────────┐               │
│ │ITask │  │ ITask    │  │ INotification│               │
│ │Repos │  │ Queue    │  │ Service      │               │
│ └──┬───┘  └────┬─────┘  └──────┬───────┘               │
│    │           │               │                        │
│    ▼           ▼               ▼                        │
│ In-Memory  Background      SignalR                      │
│ Storage    Processor       Broadcast                    │
└─────────────────────────────────────────────────────────┘
```

### 5.2 Core Interfaces

```csharp
// Task Repository - Storage abstraction
public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default);
    Task<TaskItem> AddAsync(TaskItem task, CancellationToken ct = default);
    Task<TaskItem> UpdateAsync(TaskItem task, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetQueuedTasksAsync(CancellationToken ct = default);
}

// Task Service - Business Logic
public interface ITaskService
{
    Task<TaskItem> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskItem?> GetTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetUserTasksAsync(Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> UpdateTaskAsync(Guid taskId, Guid ownerId, UpdateTaskRequest request, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
    Task<TaskItem> CancelTaskAsync(Guid taskId, Guid ownerId, CancellationToken ct = default);
}

// Notification Service - Real-time updates
public interface INotificationService
{
    Task NotifyTaskCreatedAsync(TaskItem task);
    Task NotifyTaskUpdatedAsync(TaskItem task);
    Task NotifyTaskDeletedAsync(Guid taskId, Guid ownerId);
    Task NotifyStateChangedAsync(Guid taskId, Guid ownerId, TaskState state, string? details);
    Task NotifyProgressAsync(Guid taskId, Guid ownerId, double percentage, string? details);
}

// Task Queue - Execution management
public interface ITaskQueue
{
    Task EnqueueAsync(Guid taskId, CancellationToken ct = default);
    Task<Guid?> DequeueAsync(CancellationToken ct = default);
    Task<bool> TryCancelAsync(Guid taskId, CancellationToken ct = default);
}
```

### 5.3 Task Executor Registration

```csharp
// Register executors at startup
services.AddTaskExecutor<MyCustomTaskExecutor>();  // Registers for its TaskType

// Executor implementation example
public class DataProcessingTaskExecutor : ITaskExecutor
{
    public string TaskType => "data-processing";
    
    public async Task ExecuteAsync(
        TaskItem task, 
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DataProcessingPayload>(task.Payload);
        
        for (int i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Do work...
            await Task.Delay(100, cancellationToken);
            
            progress.Report(new TaskProgressUpdate(i + 1, $"Processing item {i + 1}/100"));
        }
    }
}
```

### 5.4 Background Task Processor

```csharp
// Hosted service that processes queued tasks
public class TaskProcessorService : BackgroundService
{
    // - Dequeues tasks from ITaskQueue
    // - Resolves appropriate ITaskExecutor by task type
    // - Executes with progress tracking
    // - Updates state via ITaskService
    // - Broadcasts via INotificationService
    // - Handles cancellation tokens
    // - Configurable concurrency (default: Environment.ProcessorCount)
}
```

### 5.5 Built-in Demo Executor

Include a simple demo executor for testing:

```csharp
public class DemoTaskExecutor : ITaskExecutor
{
    public string TaskType => "demo";
    
    // Simulates work with configurable duration and steps
    // Payload: { "durationSeconds": 10, "steps": 10 }
}
```

---

## 6. Group-Based Task Parallelism

### 6.1 Overview

Tasks are assigned to **groups**, each with its own parallelism limit. This replaces the global `MaxConcurrentTasks` setting.

### 6.2 TaskGroup Entity

```csharp
public class TaskGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int MaxParallelism { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 6.3 Default Groups (Created at Startup)

| Group Name | ID | Max Parallelism | Purpose |
|------------|-----|-----------------|---------|
| `default` | `00000000-0000-0000-0000-000000000000` | 32 | Default group for unassigned tasks |
| `cpu-processing` | Auto-generated | `Environment.ProcessorCount` | CPU-bound tasks |
| `exclusive-processing` | Auto-generated | 1 | Single-task execution (e.g., radio transmission) |

### 6.4 How It Works

1. Each task has a `GroupId` (defaults to `default` group if not specified)
2. Each group has its own priority queue in `InMemoryTaskQueue`
3. `TaskProcessorService` uses per-group semaphores (`ConcurrentDictionary<Guid, SemaphoreSlim>`)
4. When a task is dequeued, the processor acquires the group's semaphore before execution
5. Tasks in the same group respect that group's `MaxParallelism` limit
6. Tasks in different groups execute independently

### 6.5 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/groups` | List all groups with task counts |
| `GET` | `/api/v1/groups/{id}` | Get group by ID |
| `POST` | `/api/v1/groups` | Create new group |
| `PUT` | `/api/v1/groups/{id}` | Update group |
| `DELETE` | `/api/v1/groups/{id}` | Delete group (cannot delete default) |

### 6.6 ITaskQueue with Groups

```csharp
public interface ITaskQueue
{
    Task EnqueueAsync(Guid taskId, Guid groupId, int priority, CancellationToken ct);
    Task<(Guid TaskId, Guid GroupId)?> DequeueAsync(CancellationToken ct);
    Task<bool> TryCancelAsync(Guid taskId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, int>> GetQueuedCountByGroupAsync(CancellationToken ct);
}
```

---

## 7. Dependency Injection & Storage Abstraction

### 7.1 Service Registration

```csharp
// Program.cs
builder.Services.AddTaskServer(options =>
{
    options.DefaultTaskTimeout = TimeSpan.FromHours(1);
});

// Storage - easily swappable
builder.Services.AddInMemoryTaskStorage();
// Future: builder.Services.AddRedisTaskStorage(connectionString);
// Future: builder.Services.AddSqlTaskStorage(connectionString);
```

**Note**: `MaxConcurrentTasks` has been replaced by per-group parallelism. Each `TaskGroup` has its own `MaxParallelism` setting.

### 7.2 In-Memory Implementation

```csharp
public class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();
    // Thread-safe implementation
}

public class InMemoryTaskQueue : ITaskQueue
{
    private readonly PriorityQueue<Guid, int> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    // Priority-based dequeuing
}
```

### 6.3 Future Storage Interface Compliance

Any storage implementation must:
- Be thread-safe
- Support async operations
- Handle concurrent access
- Implement the same interfaces

---

## 7. Project Structure

```
TaskServer/
├── src/
│   ├── TaskServer.Api/                    # Main API project
│   │   ├── Controllers/
│   │   │   └── TasksController.cs
│   │   ├── Hubs/
│   │   │   └── TaskHub.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── TaskServer.Api.csproj
│   │
│   ├── TaskServer.Core/                   # Core domain & interfaces
│   │   ├── Entities/
│   │   │   ├── TaskItem.cs
│   │   │   └── TaskState.cs
│   │   ├── Interfaces/
│   │   │   ├── ITaskRepository.cs
│   │   │   ├── ITaskService.cs
│   │   │   ├── ITaskExecutor.cs
│   │   │   ├── ITaskQueue.cs
│   │   │   └── INotificationService.cs
│   │   ├── DTOs/
│   │   │   ├── CreateTaskRequest.cs
│   │   │   ├── UpdateTaskRequest.cs
│   │   │   └── TaskResponse.cs
│   │   └── TaskServer.Core.csproj
│   │
│   ├── TaskServer.Infrastructure/         # Implementations
│   │   ├── Services/
│   │   │   ├── TaskService.cs
│   │   │   ├── NotificationService.cs
│   │   │   └── TaskProcessorService.cs
│   │   ├── Storage/
│   │   │   ├── InMemoryTaskRepository.cs
│   │   │   └── InMemoryTaskQueue.cs
│   │   ├── Executors/
│   │   │   └── DemoTaskExecutor.cs
│   │   ├── Authentication/
│   │   │   └── FirebaseAuthenticationHandler.cs
│   │   ├── Authorization/
│   │   │   ├── TaskOwnerRequirement.cs
│   │   │   └── TaskOwnerAuthorizationHandler.cs
│   │   └── TaskServer.Infrastructure.csproj
│   │
│   └── TaskServer.sln
│
├── tests/
│   ├── TaskServer.Api.Tests/
│   └── TaskServer.Infrastructure.Tests/
│
├── deploy/
│   ├── Dockerfile
│   ├── k3s/
│   │   ├── deployment.yaml
│   │   ├── service.yaml
│   │   ├── configmap.yaml
│   │   └── secret.yaml
│   └── docker-compose.yaml               # For local development
│
└── README.md
```

---

## 8. Kubernetes (K3s) Deployment

### 8.1 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TaskServer.Api.dll"]
```

### 8.2 Kubernetes Manifests

**Deployment**: Include health checks, resource limits, and environment configuration
**Service**: ClusterIP service for internal access, or LoadBalancer/Ingress for external
**ConfigMap**: Non-sensitive configuration
**Secret**: JWT signing keys and sensitive data

### 8.3 Health Checks

```csharp
// Endpoints
// GET /health/live   - Liveness probe
// GET /health/ready  - Readiness probe (checks dependencies)
```

---

## 9. Configuration

### 9.1 appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Authentication": {
    "Firebase": {
      "ProjectId": "intelpro-23055",
      "ValidAudience": "intelpro-23055",
      "ValidIssuer": "https://securetoken.google.com/intelpro-23055"
    }
  },
  "TaskServer": {
    "MaxConcurrentTasks": 4,
    "DefaultTaskTimeoutMinutes": 60,
    "TaskQueuePollingIntervalMs": 100
  },
  "SignalR": {
    "EnableDetailedErrors": false,
    "KeepAliveIntervalSeconds": 15,
    "ClientTimeoutSeconds": 30
  }
}
```

### 9.2 Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | Production |
| `FIREBASE_PROJECT_ID` | Firebase project ID | intelpro-23055 |
| `TASK_MAX_CONCURRENT` | Max parallel tasks | 4 |

---

## 10. Error Handling

### 10.1 Standard Error Response

```csharp
public record ErrorResponse(
    string Code,
    string Message,
    string? Details = null,
    string? TraceId = null
);
```

### 10.2 HTTP Status Codes

| Status | Usage |
|--------|-------|
| 200 | Success |
| 201 | Created |
| 204 | No Content (delete) |
| 400 | Bad Request (validation) |
| 401 | Unauthorized (no/invalid token) |
| 403 | Forbidden (not owner) |
| 404 | Not Found |
| 409 | Conflict (invalid state transition) |
| 500 | Internal Server Error |

### 10.3 Global Exception Handler

Implement middleware to catch and format all exceptions consistently.

---

## 11. Testing Requirements

### 11.1 Unit Tests

- Task service business logic
- Authorization handlers
- Task executors

### 11.2 Integration Tests

- REST API endpoints
- SignalR hub methods
- End-to-end task lifecycle

---

## 12. Open Source Alternatives Considered

| Library | Pros | Cons | Decision |
|---------|------|------|----------|
| **Hangfire** | Mature, persistent | Heavy, requires SQL | Too complex |
| **Quartz.NET** | Scheduling features | Overkill for simple tasks | Not needed |
| **Coravel** | Lightweight | Limited features | Consider for future |

**Decision**: Custom implementation for maximum simplicity and control. The architecture allows easy integration of these libraries later if needed.

---

## 13. Implementation Priorities

### Phase 1 - MVP
1. Project structure and solution setup
2. Core domain models and interfaces
3. In-memory storage implementation
4. REST API with CRUD operations
5. Basic JWT authentication (Firebase)
6. Swagger documentation

### Phase 2 - Task Execution
7. Task executor framework
8. Background processor service
9. Demo task executor
10. Task queue with priority support

### Phase 3 - Real-time
11. SignalR hub implementation
12. Notification service
13. Progress and state change broadcasting

### Phase 4 - Production Ready
14. Authorization policies
15. Kubernetes manifests
16. Health checks
17. Logging and monitoring
18. Unit and integration tests

---

## 14. Commands

```bash
# Create solution
dotnet new sln -n TaskServer

# Create projects
dotnet new classlib -n TaskServer.Core -f net10.0
dotnet new classlib -n TaskServer.Infrastructure -f net10.0
dotnet new webapi -n TaskServer.Api -f net10.0

# Add to solution
dotnet sln add src/TaskServer.Core
dotnet sln add src/TaskServer.Infrastructure
dotnet sln add src/TaskServer.Api

# Add package references (Api project)
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Asp.Versioning.Mvc
dotnet add package Asp.Versioning.Mvc.ApiExplorer
dotnet add package Swashbuckle.AspNetCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

# Add project references
dotnet add TaskServer.Api reference TaskServer.Core TaskServer.Infrastructure
dotnet add TaskServer.Infrastructure reference TaskServer.Core
```

---

## Appendix A: Sample API Calls

### Create Task
```http
POST /api/v1/tasks
Authorization: Bearer <jwt_token>
Content-Type: application/json

{
  "priority": 10,
  "type": "demo",
  "payload": "{\"durationSeconds\": 30, \"steps\": 10}"
}
```

### SignalR Connection
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/tasks", { accessTokenFactory: () => jwtToken })
    .build();

connection.on("OnProgress", (taskId, percentage, details) => {
    console.log(`Task ${taskId}: ${percentage}% - ${details}`);
});

connection.on("OnStateChanged", (taskId, newState, details) => {
    console.log(`Task ${taskId} is now ${newState}`);
});

await connection.start();
await connection.invoke("SubscribeToAllUserTasks");
```

---

## 15. Implementation Decisions

This section documents key architectural decisions made during implementation.

### 15.1 Task Cancellation Architecture

**Problem**: `TaskService.CancelTaskAsync()` only updated the database state but did not signal the running task's CancellationToken.

**Solution**: Created `ITaskCancellationService` interface:

```csharp
public interface ITaskCancellationService
{
    bool TryCancelRunningTask(Guid taskId);
}
```

- `TaskProcessorService` implements this interface and maintains a dictionary of running task CancellationTokenSources
- `TaskService` injects `ITaskCancellationService` and calls `TryCancelRunningTask()` when cancelling an Executing task
- Registered as singleton to ensure single instance tracks all running tasks

### 15.2 Progress Payload Extension

**Problem**: `TaskProgressUpdate` only supported percentage and string details, not structured data.

**Solution**: Extended `TaskProgressUpdate` record with optional JSON payload:

```csharp
public record TaskProgressUpdate(
    double Percentage,
    string? Details = null,
    string? PayloadJson = null  // NEW: Structured JSON data
);
```

Changes propagated to:
- `TaskItem.ProgressPayload` property (entity)
- `TaskResponse.ProgressPayload` property (DTO)
- `INotificationService.NotifyProgressAsync()` - added payload parameter
- `ITaskHubClient.OnProgress()` - added payload parameter
- Frontend `ProgressEvent` interface

### 15.3 Queue Priority and FIFO Ordering

**Problem**: Standard priority queue doesn't preserve insertion order for same-priority tasks.

**Solution**: Implemented `(priority, sequenceNumber)` composite ordering:

```csharp
private readonly PriorityQueue<QueuedTask, (int Priority, long Sequence)> _queue;
private long _sequenceNumber = 0;

// On enqueue:
_queue.Enqueue(task, (-priority, Interlocked.Increment(ref _sequenceNumber)));
```

- Negative priority ensures higher values execute first
- Sequence number ensures FIFO for same priority
- Thread-safe via `Interlocked.Increment`

### 15.4 Plugin Architecture

**Decisions**:
- Plugins are separate class library projects in `src/plugins/`
- Each plugin references only `TaskServer.Core` for minimal coupling
- Dynamic loading via reflection at startup:

```csharp
var assembly = Assembly.LoadFrom(dllPath);
var executorTypes = assembly.GetTypes()
    .Where(t => typeof(ITaskExecutor).IsAssignableFrom(t)
             && !t.IsInterface && !t.IsAbstract);
```

- Plugin DLLs are copied to `plugins/` folder in output directory
- Graceful handling of unloadable DLLs (silently skipped)

**Built-in Plugins**:
| Plugin | Task Type | Description |
|--------|-----------|-------------|
| CountDown | `countdown` | Counts down from specified duration, reports remaining seconds |
| RollDice | `rolldice` | Rolls two dice until target combination (max 100 attempts) |

### 15.5 Frontend Task Form System

**Architecture**:
- `BaseTaskFormComponent` - Abstract directive defining form contract:
  - `form: FormGroup` - Reactive form
  - `getPayload(): object` - Serializes form to task payload
  - `getCustomTaskType(): string | null` - Override for generic forms
  - `isValid(): boolean` - Form validation

- `TaskFormRegistryService` - Maps task types to Angular components
- `TaskFormHostComponent` - Dynamic component loading via `ViewContainerRef.createComponent()`
- Registration via `APP_INITIALIZER` pattern

**Registered Forms**:
| Task Type | Component | Description |
|-----------|-----------|-------------|
| countdown | CountdownFormComponent | Duration input with validation |
| generic | GenericFormComponent | Manual type + JSON payload input |
| rolldice | RollDiceFormComponent | Dice selection with odds calculator |

### 15.6 Compile-Time Authentication Switching

Authentication is controlled by `#if DEBUG` preprocessor directives at compile time:

**Debug Builds** (`dotnet run` or `dotnet build`):
- Controllers use `[AllowAnonymous]` attribute
- Uses fixed test user ID: `00000000-0000-0000-0000-000000000001`
- No JWT token required for any operations

**Release Builds** (`dotnet build -c Release`):
- Controllers use `[Authorize]` attribute
- Extracts user ID from JWT claims (`user_id` or `sub`)
- Requires valid Firebase JWT token

**Implementation Pattern**:
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/tasks")]
#if DEBUG
[AllowAnonymous]
#else
[Authorize]
#endif
public class TasksController : ControllerBase
{
    #if DEBUG
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private Guid GetUserId() => TestUserId;
    #else
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? throw new UnauthorizedAccessException("User ID not found in token");
        // ... JWT parsing logic
    }
    #endif
}
```

**Benefits**:
- Single source of truth (no duplicate controllers)
- No runtime overhead (compile-time switching)
- Secure by default (Release builds require auth automatically)

### 15.7 Delete Functionality

**Behavior**:
- Tasks can be deleted in any state
- If task is `Executing`, cancellation is triggered first
- `OnTaskDeleted` SignalR event broadcast to all subscribed clients
- Task removed from repository and queue (if queued)

---

## 16. Hierarchical Task System

### 16.1 Overview

Tasks support parent-child relationships with unlimited nesting depth, enabling complex workflows where parent tasks orchestrate child tasks. **Children can be ANY task type** - countdown, roll-dice, demo, or even nested hierarchical tasks.

### 16.2 TaskItem Hierarchical Properties

```csharp
public class TaskItem
{
    // ... existing properties ...

    // Hierarchical task properties
    public Guid? ParentTaskId { get; set; }      // Reference to parent (null = root task)
    public double Weight { get; set; } = 1.0;    // Progress contribution weight
    public bool SubtaskParallelism { get; set; } = true; // true = parallel, false = sequential
    public int ChildCount { get; set; }          // Number of immediate children
}
```

### 16.3 Progress Aggregation

Parent task progress is calculated as weighted average of immediate children:

```
Parent Progress = Σ(child.progress × child.weight) / Σ(child.weight)
```

**Key behaviors:**
- Aggregation only considers immediate children (not grandchildren)
- Each child's own progress already includes its subtree aggregation (bubbles up recursively)
- Weight normalization ensures proportional contribution
- Updates propagate up through entire ancestor chain

```csharp
public interface IProgressAggregationService
{
    Task<double> AggregateProgressAsync(Guid parentTaskId);
    Task UpdateAncestorProgressAsync(Guid childTaskId);
}
```

### 16.4 Execution Behavior

**Parent Task Orchestration** (`TaskProcessorService.ExecuteParentTaskAsync`):

1. **Parallel Execution** (`SubtaskParallelism = true`):
   - All children enqueued immediately
   - Children execute concurrently (respecting group parallelism limits)
   - Parent monitors completion via SignalR events

2. **Sequential Execution** (`SubtaskParallelism = false`):
   - Children enqueued one at a time
   - Each child must complete before next starts
   - Failure stops remaining children

3. **Group Parallelism**:
   - Parent and children can use same or different groups
   - Group `MaxParallelism` always respected
   - Different groups execute independently

### 16.5 Cascade Operations

**Cancel Subtree** - Cancels task and all descendants (processes leaves first):
```csharp
Task CancelTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct);
```

**Delete Subtree** - Atomically deletes entire subtree:
```csharp
Task<bool> DeleteTaskSubtreeAsync(Guid taskId, Guid ownerId, CancellationToken ct);
```

### 16.6 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/v1/tasks/hierarchy` | Create parent + children atomically |
| `GET` | `/api/v1/tasks/{id}/children` | Get immediate child tasks |
| `POST` | `/api/v1/tasks/{id}/cancel-subtree` | Cancel task and all descendants |
| `DELETE` | `/api/v1/tasks/{id}/subtree` | Delete task and all descendants |

### 16.7 CreateTaskHierarchyRequest

```csharp
public record CreateTaskHierarchyRequest(
    CreateTaskNodeRequest ParentTask,
    List<CreateTaskHierarchyRequest> ChildTasks = null
);

public record CreateTaskNodeRequest(
    string Type,
    int Priority = 5,
    string Payload = "{}",
    string? GroupId = null,
    double Weight = 1.0,
    bool SubtaskParallelism = true
);
```

**Example Request:**
```json
{
  "parentTask": {
    "type": "hierarchical-parent",
    "priority": 5,
    "payload": "{}",
    "subtaskParallelism": true
  },
  "childTasks": [
    {
      "parentTask": { "type": "countdown", "payload": "{\"durationInSeconds\":10}", "weight": 1.0 },
      "childTasks": []
    },
    {
      "parentTask": { "type": "rolldice", "payload": "{\"desiredDice1\":6,\"desiredDice2\":6}", "weight": 2.0 },
      "childTasks": []
    },
    {
      "parentTask": {
        "type": "hierarchical-parent",
        "payload": "{}",
        "weight": 3.0,
        "subtaskParallelism": false
      },
      "childTasks": [
        { "parentTask": { "type": "countdown", "payload": "{\"durationInSeconds\":5}" }, "childTasks": [] }
      ]
    }
  ]
}
```

### 16.8 TaskExecutorBase Lifecycle Hooks

```csharp
public abstract class TaskExecutorBase : ITaskExecutor
{
    public abstract string TaskType { get; }

    public abstract Task ExecuteAsync(TaskItem task, IProgress<TaskProgressUpdate> progress, CancellationToken ct);

    // Override these for custom subtask handling
    public virtual void OnSubtaskProgress(TaskItem parent, TaskItem child, TaskProgressUpdate progress) { }
    public virtual void OnSubtaskStateChange(TaskItem parent, TaskItem child, TaskStateChange change) { }
    public virtual void OnAllSubtasksSuccess(TaskItem parent, IReadOnlyList<TaskItem> children) { }
}
```

### 16.9 Built-in Hierarchical Executors

| Executor | Task Type | Description |
|----------|-----------|-------------|
| `HierarchicalParentExecutor` | `hierarchical-parent` | Generic parent for orchestrating mixed child types |
| `SimpleHierarchicalExecutor` | `simple-hierarchical` | Simple hierarchical with configurable duration/steps |

---

## 17. Auth Token Propagation

Tasks can make authenticated HTTP calls to downstream microservices using the JWT token captured at task creation time.

### 17.1 How It Works

1. **Token Capture**: Controller extracts JWT from `Authorization` header and passes to `ITaskService`
2. **Token Storage**: Token stored in `TaskItem.AuthToken` property
3. **Token Access**: Executors access via `task.AuthToken`
4. **HttpClient Factory**: `ITaskHttpClientFactory.CreateClient(task.AuthToken)` creates pre-configured client

### 17.2 Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TaskItem.AuthToken` | Core/Entities | Stores captured JWT token |
| `ITaskHttpClientFactory` | Core/Interfaces | Creates authenticated HttpClient |
| `TaskHttpClientFactory` | Infrastructure/Services | Implementation |
| `MicroserviceCallExecutor` | Infrastructure/Executors | Built-in HTTP call executor |

### 17.3 Built-in microservice-call Task

**Task Type**: `microservice-call`

**Payload**:
```json
{
  "url": "https://api.example.com/endpoint",
  "method": "GET",
  "requestBody": "{\"key\":\"value\"}",
  "contentType": "application/json",
  "retryCount": 3,
  "retryDelayMs": 1000,
  "timeoutSeconds": 30
}
```

**Progress Payload**:
```json
{
  "phase": "completed",
  "attempt": 1,
  "maxAttempts": 4,
  "statusCode": 200,
  "responseBody": "{\"result\":\"success\"}",
  "hasAuthToken": true
}
```

### 17.4 Example Custom Executor

```csharp
public class MyApiExecutor : ITaskExecutor
{
    private readonly ITaskHttpClientFactory _httpClientFactory;

    public MyApiExecutor(ITaskHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string TaskType => "my-api-call";

    public async Task ExecuteAsync(TaskItem task, IProgress<TaskProgressUpdate> progress, CancellationToken ct)
    {
        // Create HttpClient with user's auth token
        using var client = _httpClientFactory.CreateClient(task.AuthToken, "https://api.example.com");

        // Authorization: Bearer <token> is automatically set
        var response = await client.GetAsync("/api/data", ct);
        // ...
    }
}
```

---

## 18. Frontend Features (Angular)

### 18.1 Real-time History Tracking

Root hierarchical tasks display live history of child activity:

**Progress History Box:**
- Shows last 20 child progress updates (scrollable, 3 lines visible)
- Format: `HH:mm:ss taskType displayText` (e.g., `15:18:54 rolldice Rolled 6-6!`)
- Excludes aggregated parent progress (only shows actual child updates)

**State Change History Box:**
- Shows last 20 child state transitions
- Format: `HH:mm:ss taskIdShort... taskType newState`
- Color-coded states (green=Completed, blue=Executing, red=Errored)

**Implementation:**
- `TaskStoreService` maintains `progressHistoryMap` and `stateHistoryMap` keyed by root task ID
- `ProgressHistoryEntry` and `StateChangeHistoryEntry` interfaces in `task.model.ts`
- `TaskTreeNode` includes `progressHistory` and `stateChangeHistory` arrays (populated for level 0 only)

### 18.2 SignalR Connection Resilience

**Automatic Reconnect:**
- Built-in SignalR retry: 0s, 1s, 2s, 5s, 10s, 30s delays
- Continues attempting until connection restored or manually stopped

**Manual Reconnect Fallback:**
- Activates after automatic reconnect exhausts its attempts
- Up to 10 additional attempts with increasing delays: 1s, 2s, 5s, 10s, 15s, 30s, 60s
- User can click connection indicator to force manual reconnect

**Connection Status Indicator:**
- Visual status in toolbar (green/yellow/red dot with icons)
- `connected`: Green dot, wifi icon
- `connecting`/`reconnecting`: Yellow pulsing dot, spinning sync icon
- `disconnected`: Red dot, wifi_off icon
- Tooltip shows connection details and last error
- Click to manually reconnect when disconnected or stalled

**Auto-refresh:**
- Task list automatically refreshes when connection is restored
- `reconnected$` Subject emits when connection restored after disconnection

### 18.3 UI Enhancements

**Background Color by Nesting Level:**
- Level 0 (root): #ffffff (white)
- Level 1: #fafafa
- Level 2: #f5f5f5
- Level 3: #eeeeee
- Level 4: #e8e8e8
- Level 5+: #e0e0e0

**Mobile Responsive Design:**
- Breakpoints at 768px, 600px, 400px
- Reduced indentation on mobile
- Badges and timestamps resize
- History boxes stack vertically on narrow screens
- Task ID and non-essential info hidden on very small screens

**Simplified Delete/Cancel:**
- Single "Delete" and "Cancel" buttons (no dropdown menus)
- Automatically uses subtree actions for hierarchical tasks
- Uses single-task actions for leaf tasks
- Consistent naming regardless of task type

### 18.4 State Management

`TaskStoreService` uses reactive patterns:
- `tasksMap$`: BehaviorSubject<Map<string, TaskResponse>> - flat task storage
- `taskTree$`: Observable<TaskTreeNode[]> - hierarchical tree built from flat map
- `expandedTaskIds$`: BehaviorSubject<Set<string>> - expand/collapse state
- `progressHistoryMap$`: BehaviorSubject<Map<string, ProgressHistoryEntry[]>> - child progress history
- `stateHistoryMap$`: BehaviorSubject<Map<string, StateChangeHistoryEntry[]>> - child state changes

---

## Appendix B: Updated Interface Signatures

### ITaskExecutor (Central Interface)
```csharp
public interface ITaskExecutor
{
    string TaskType { get; }
    Task ExecuteAsync(TaskItem task, IProgress<TaskProgressUpdate> progress, CancellationToken ct);
}
```

### ITaskQueue
```csharp
public interface ITaskQueue
{
    Task EnqueueAsync(Guid taskId, int priority, CancellationToken ct = default);
    Task<Guid?> DequeueAsync(CancellationToken ct = default);
    Task<bool> TryCancelAsync(Guid taskId, CancellationToken ct = default);
}
```

### INotificationService
```csharp
public interface INotificationService
{
    Task NotifyTaskCreatedAsync(TaskItem task);
    Task NotifyTaskUpdatedAsync(TaskItem task);
    Task NotifyTaskDeletedAsync(Guid taskId, Guid ownerId);
    Task NotifyStateChangedAsync(Guid taskId, Guid ownerId, TaskState state, string? details);
    Task NotifyProgressAsync(Guid taskId, Guid ownerId, double percentage, string? details, string? payload);
}
```

### ITaskCancellationService
```csharp
public interface ITaskCancellationService
{
    bool TryCancelRunningTask(Guid taskId);
}
```

### IProgressAggregationService
```csharp
public interface IProgressAggregationService
{
    Task<double> AggregateProgressAsync(Guid parentTaskId);
    Task UpdateAncestorProgressAsync(Guid childTaskId);
}
```

### ITaskHubClient (SignalR)
```csharp
public interface ITaskHubClient
{
    Task OnTaskCreated(TaskResponse task);
    Task OnTaskUpdated(TaskResponse task);
    Task OnTaskDeleted(Guid taskId);
    Task OnStateChanged(Guid taskId, string newState, string? details);
    Task OnProgress(Guid taskId, double percentage, string? details, string? payload);
}
```