# Pulstrate

https://www.youtube.com/watch?v=COGAi8NxVtU

**I always wanted something like this.**

A task management system that wasn't overkill, but wasn't too simple either. Something I could understand in an afternoon, extend by Monday, and trust in production by Friday.

## When Hangfire is Too Much, But setTimeout Isn't Enough

**Pulstrate** sits in the sweet spot: a bootstrap project with just enough sophistication to handle real-world complexity, yet simple enough that you actually understand how it works.

### What You Get

- ✨ **Hierarchical Tasks** - Build complex workflows with parent-child relationships and unlimited nesting
- ⚡ **Smart Parallelism** - Configurable concurrency limits that actually saturate your resources efficiently
- 📊 **Real-Time Progress** - SignalR-powered live updates for outstanding user experience
- 🔌 **Plugin Architecture** - Drop in custom task executors, no framework wrestling required
- 🎯 **Boolean Logic Workflows** - Sequential, parallel, or mixed execution patterns out of the box
- 🔥 **Modern Stack** - .NET 10, Angular 21, SignalR - battle-tested technologies

### The Philosophy

This isn't about reinventing the wheel. It's about having a wheel that fits your car, that you can fix yourself, and that doesn't require a PhD to modify.

- Need progress bars that actually reflect reality? ✓
- Want to coordinate 20 parallel tasks across different resource groups? ✓
- Need it yesterday and working tomorrow? ✓

### Why spend money and time on a complex system when 99% of the functionality is right here under your nose?

**Open source. Ready to run. Built to extend.**

A full-stack task management and execution system with real-time progress tracking, hierarchical task orchestration, and a plugin-based executor architecture.

## Features

- **Task Execution Engine**: Background service that processes tasks with configurable parallelism
- **Real-Time Updates**: SignalR-based live progress tracking and state notifications
- **Hierarchical Tasks**: Parent-child relationships with unlimited nesting depth
- **Boolean Logic Workflows**: AND/OR/JOIN patterns via sequential/parallel child execution
- **Plugin System**: Dynamically loaded task executors from DLL plugins
- **Group-Based Parallelism**: Independent parallelism limits per task group
- **Firebase Authentication**: JWT-based authentication with compile-time debug/release switching

## Technology Stack

| Layer | Technology |
|-------|------------|
| Backend API | ASP.NET Core 10 |
| Real-Time | SignalR |
| Frontend | Angular 21 |
| UI Components | Angular Material |
| State Management | RxJS BehaviorSubject |

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- npm (included with Node.js)

### Running the Demo

**Terminal 1 - Backend:**
```bash
cd backend/task-server
dotnet run --project src/TaskServer.Api
```

**Terminal 2 - Frontend:**
```bash
cd frontend/task-manager
npm install
npm start
```

**Access Points:**
| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| Backend API | http://localhost:5128 |
| Swagger UI | http://localhost:5128/swagger |
| SignalR Hub | http://localhost:5128/hubs/tasks |

---

## Using the Angular UI Demo

### Creating Simple Tasks

1. Click **"Create Task"** button
2. Select a task type from the dropdown:
   - **Countdown**: Timer that counts down from specified seconds
   - **Roll Dice**: Rolls dice until target combination is hit
   - **Demo**: Simulates work with configurable steps
3. Configure task parameters
4. Select an execution group (controls parallelism)
5. Click **"Create"** and watch real-time progress

### Creating Hierarchical Tasks

1. Select **"Hierarchical Task"** from the task type dropdown
2. Configure the parent task name
3. Toggle **Parallel/Sequential** execution mode
4. Click **"Add Child"** to add child tasks
5. For each child:
   - Select task type (can be any type, including nested hierarchical)
   - Configure task-specific parameters
   - Set **Weight** for progress contribution (default: 1.0)
   - Select execution **Group**
6. Click **"Create"** to atomically create the entire hierarchy

### Tree View Features

- **Expand/Collapse**: Click the arrow on parent tasks
- **Expand All / Collapse All**: Buttons at the top of the task list
- **Aggregated Progress**: Parent tasks show weighted average of children
- **Cascade Operations**: Cancel or delete entire subtrees

---

## Boolean Logic with Hierarchical Tasks

The hierarchical task system enables full boolean workflow logic through composition:

| Pattern | Configuration | Behavior |
|---------|---------------|----------|
| **Sequential (AND)** | `subtaskParallelism: false` | Each child must complete before the next starts |
| **Parallel (Fork)** | `subtaskParallelism: true` | All children run concurrently |
| **Join (Wait All)** | Any parallelism | Parent completes only when ALL children complete |
| **Complex Workflows** | Nested hierarchical tasks | Mix sequential and parallel at different levels |

### Example: ETL Pipeline

```
Root Parent (sequential)
├── Step 1: Extract (countdown task)
├── Step 2: Transform (hierarchical-parent, parallel)
│   ├── Transform Part A (demo task)
│   ├── Transform Part B (demo task)
│   └── Transform Part C (demo task)
└── Step 3: Load (countdown task)
```

**Execution Flow:**
1. Extract runs first (sequential parent)
2. All Transform parts run in parallel (parallel child)
3. Load waits for all transforms to complete
4. Root completes when Load finishes

### Progress Aggregation

Parent progress is calculated as a weighted average of immediate children:

```
Parent Progress = Σ(child.progress × child.weight) / Σ(child.weight)
```

Use weights to reflect relative task importance in overall progress.

### Dynamic Subtask Addition

Executors can dynamically add subtasks during execution by overriding `OnSubtaskStateChangeAsync`. This enables reactive workflows like:
- **Retry patterns**: Automatically retry failed subtasks
- **Saga patterns**: Add compensating actions on failure
- **Conditional workflows**: Add follow-up tasks based on results

```csharp
public class RetryingParentExecutor : TaskExecutorBase
{
    public override string TaskType => "retrying-parent";

    public override Task<IReadOnlyList<CreateTaskRequest>?> OnSubtaskStateChangeAsync(
        TaskItem parent, TaskItem child, TaskStateChange change)
    {
        if (change.NewState == TaskState.Errored)
        {
            // Return new subtasks to add
            return Task.FromResult<IReadOnlyList<CreateTaskRequest>?>(
                new List<CreateTaskRequest> {
                    new(null, child.Priority, child.Type, child.Payload, child.GroupId, child.Weight, null)
                });
        }
        return Task.FromResult<IReadOnlyList<CreateTaskRequest>?>(null);
    }
}
```

**Key behaviors:**
- Only subtasks of the current parent can be added (not root tasks)
- New subtasks inherit parent's auth token
- Progress recalculates including new children (may decrease temporarily)
- Parallelism settings remain unchanged from creation

---

## REST API Reference

### Tasks Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/tasks` | List all tasks |
| GET | `/api/v1/tasks/{id}` | Get single task |
| POST | `/api/v1/tasks` | Create task |
| POST | `/api/v1/tasks/{id}/cancel` | Cancel task |
| DELETE | `/api/v1/tasks/{id}` | Delete task |

### Hierarchical Task Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/tasks/hierarchy` | Create parent + children atomically |
| GET | `/api/v1/tasks/{id}/children` | Get immediate children |
| POST | `/api/v1/tasks/{id}/cancel-subtree` | Cancel task and all descendants |
| DELETE | `/api/v1/tasks/{id}/subtree` | Delete entire subtree |

### Groups Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/groups` | List all groups |
| GET | `/api/v1/groups/{id}` | Get single group |
| POST | `/api/v1/groups` | Create group |
| PUT | `/api/v1/groups/{id}` | Update group |
| DELETE | `/api/v1/groups/{id}` | Delete group |

### Health Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health/live` | Liveness probe |
| GET | `/health/ready` | Readiness probe |

> **Note**: Authentication is controlled by compile-time `#if DEBUG` directives. Debug builds use `[AllowAnonymous]`, Release builds require JWT authentication.

---

## Using Swagger

1. Start the backend server
2. Navigate to http://localhost:5128/swagger
3. Explore all available endpoints
4. Click **"Try it out"** to test requests interactively
5. View request/response schemas and examples

---

## Firebase Authentication

### Backend Configuration

Update `appsettings.json` with your Firebase project:

```json
{
  "Authentication": {
    "Firebase": {
      "ProjectId": "your-firebase-project-id",
      "ValidAudience": "your-firebase-project-id",
      "ValidIssuer": "https://securetoken.google.com/your-firebase-project-id"
    }
  }
}
```

### Setup Steps

1. Create a project at [Firebase Console](https://console.firebase.google.com)
2. Go to **Project Settings** and copy the **Project ID**
3. Enable **Authentication** and configure sign-in methods
4. Update `appsettings.json` with your Project ID

### Using Authenticated Endpoints

**REST API:**
```http
GET /api/v1/tasks
Authorization: Bearer <firebase-jwt-token>
```

**SignalR:**
```javascript
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/tasks?access_token=' + firebaseToken)
  .build();
```

### Development Mode

Authentication is controlled by compile-time `#if DEBUG` preprocessor directives:
- **Debug builds** (`dotnet run` or `dotnet build`): `[AllowAnonymous]` - no JWT required
- **Release builds** (`dotnet build -c Release`): `[Authorize]` - JWT required

---

## Sample Angular Services

### Task API Service

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface TaskResponse {
  id: string;
  ownerId: string;
  groupId: string;
  groupName: string;
  priority: number;
  type: string;
  payload: string;
  state: 'Queued' | 'Executing' | 'Completed' | 'Cancelled' | 'Errored' | 'Terminated';
  progress: number;
  progressDetails: string | null;
  progressPayload: string | null;
  stateDetails: string | null;
  createdAt: string;
  updatedAt: string;
  startedAt: string | null;
  completedAt: string | null;
  parentTaskId: string | null;
  weight: number;
  subtaskParallelism: boolean;
  childCount: number;
}

export interface CreateTaskRequest {
  id?: string;
  priority: number;
  type: string;
  payload: string;
  groupId?: string;
  parentTaskId?: string;
  weight?: number;
  subtaskParallelism?: boolean;
}

export interface CreateTaskHierarchyRequest {
  parentTask: CreateTaskRequest;
  childTasks: CreateTaskHierarchyRequest[];
}

@Injectable({ providedIn: 'root' })
export class TaskApiService {
  private readonly baseUrl = `${environment.apiUrl}/api/v1/tasks`;

  constructor(private http: HttpClient) {}

  getTasks(): Observable<TaskResponse[]> {
    return this.http.get<TaskResponse[]>(this.baseUrl);
  }

  getTask(id: string): Observable<TaskResponse> {
    return this.http.get<TaskResponse>(`${this.baseUrl}/${id}`);
  }

  createTask(request: CreateTaskRequest): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(this.baseUrl, request);
  }

  createTaskHierarchy(request: CreateTaskHierarchyRequest): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(`${this.baseUrl}/hierarchy`, request);
  }

  getChildTasks(parentId: string): Observable<TaskResponse[]> {
    return this.http.get<TaskResponse[]>(`${this.baseUrl}/${parentId}/children`);
  }

  cancelTask(id: string): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(`${this.baseUrl}/${id}/cancel`, {});
  }

  cancelTaskSubtree(id: string): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(`${this.baseUrl}/${id}/cancel-subtree`, {});
  }

  deleteTask(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  deleteTaskSubtree(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/subtree`);
  }
}
```

### SignalR Service

```typescript
import { Injectable } from '@angular/core';
import { Subject, BehaviorSubject } from 'rxjs';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { environment } from '../environments/environment';
import { TaskResponse } from './task-api.service';

export interface StateChangedEvent {
  taskId: string;
  newState: string;
  details: string | null;
}

export interface ProgressEvent {
  taskId: string;
  percentage: number;
  details: string | null;
  payload: string | null;
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hubConnection: HubConnection | null = null;

  // Connection state observable
  readonly connectionState$ = new BehaviorSubject<HubConnectionState>(HubConnectionState.Disconnected);

  // Task event observables
  readonly taskCreated$ = new Subject<TaskResponse>();
  readonly taskUpdated$ = new Subject<TaskResponse>();
  readonly taskDeleted$ = new Subject<string>();
  readonly stateChanged$ = new Subject<StateChangedEvent>();
  readonly progress$ = new Subject<ProgressEvent>();

  async connect(accessToken?: string): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return;
    }

    let url = `${environment.signalRUrl}/hubs/tasks`;
    if (accessToken) {
      url += `?access_token=${accessToken}`;
    }

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build();

    // Register event handlers
    this.hubConnection.on('OnTaskCreated', (task: TaskResponse) => {
      this.taskCreated$.next(task);
    });

    this.hubConnection.on('OnTaskUpdated', (task: TaskResponse) => {
      this.taskUpdated$.next(task);
    });

    this.hubConnection.on('OnTaskDeleted', (taskId: string) => {
      this.taskDeleted$.next(taskId);
    });

    this.hubConnection.on('OnStateChanged', (taskId: string, newState: string, details: string | null) => {
      this.stateChanged$.next({ taskId, newState, details });
    });

    this.hubConnection.on('OnProgress', (taskId: string, percentage: number, details: string | null, payload: string | null) => {
      this.progress$.next({ taskId, percentage, details, payload });
    });

    // Connection state handlers
    this.hubConnection.onreconnecting(() => {
      this.connectionState$.next(HubConnectionState.Reconnecting);
    });

    this.hubConnection.onreconnected(() => {
      this.connectionState$.next(HubConnectionState.Connected);
    });

    this.hubConnection.onclose(() => {
      this.connectionState$.next(HubConnectionState.Disconnected);
    });

    // Start connection
    try {
      await this.hubConnection.start();
      this.connectionState$.next(HubConnectionState.Connected);
    } catch (err) {
      console.error('SignalR connection error:', err);
      this.connectionState$.next(HubConnectionState.Disconnected);
      throw err;
    }
  }

  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
      this.connectionState$.next(HubConnectionState.Disconnected);
    }
  }
}
```

### Usage Example

```typescript
@Component({
  selector: 'app-task-monitor',
  template: `
    <div *ngFor="let task of tasks$ | async">
      {{ task.type }}: {{ task.progress }}%
    </div>
  `
})
export class TaskMonitorComponent implements OnInit, OnDestroy {
  tasks$ = new BehaviorSubject<TaskResponse[]>([]);
  private destroy$ = new Subject<void>();

  constructor(
    private taskApi: TaskApiService,
    private signalR: SignalRService
  ) {}

  async ngOnInit() {
    // Connect to SignalR
    await this.signalR.connect();

    // Load initial tasks
    this.taskApi.getTasks().subscribe(tasks => {
      this.tasks$.next(tasks);
    });

    // Subscribe to real-time updates
    this.signalR.taskCreated$.pipe(takeUntil(this.destroy$)).subscribe(task => {
      const tasks = [...this.tasks$.value, task];
      this.tasks$.next(tasks);
    });

    this.signalR.progress$.pipe(takeUntil(this.destroy$)).subscribe(event => {
      const tasks = this.tasks$.value.map(t =>
        t.id === event.taskId
          ? { ...t, progress: event.percentage, progressDetails: event.details }
          : t
      );
      this.tasks$.next(tasks);
    });

    this.signalR.stateChanged$.pipe(takeUntil(this.destroy$)).subscribe(event => {
      const tasks = this.tasks$.value.map(t =>
        t.id === event.taskId
          ? { ...t, state: event.newState as any, stateDetails: event.details }
          : t
      );
      this.tasks$.next(tasks);
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
    this.signalR.disconnect();
  }
}
```

---

## Creating Custom Tasks

### Backend: Task Executor

Create a new class implementing `ITaskExecutor`:

```csharp
using System.Text.Json;
using TaskServer.Core.Entities;
using TaskServer.Core.Interfaces;

public class MyCustomTaskExecutor : ITaskExecutor
{
    public string TaskType => "my-custom-task";

    public async Task ExecuteAsync(
        TaskItem task,
        IProgress<TaskProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        // Parse payload from JSON
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<MyTaskPayload>(task.Payload, options)
                      ?? new MyTaskPayload();

        // Execute with progress reporting
        for (int i = 0; i < payload.Steps; i++)
        {
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();

            // Do actual work
            await DoWorkAsync(payload.CustomSetting);

            // Report progress
            var percentage = (i + 1) * 100.0 / payload.Steps;
            progress.Report(new TaskProgressUpdate(
                percentage,
                $"Completed step {i + 1} of {payload.Steps}",
                JsonSerializer.Serialize(new {
                    currentStep = i + 1,
                    totalSteps = payload.Steps
                })
            ));
        }
    }

    private async Task DoWorkAsync(string setting)
    {
        // Your task logic here
        await Task.Delay(1000);
    }
}

public class MyTaskPayload
{
    public int Steps { get; set; } = 5;
    public string CustomSetting { get; set; } = "default";
    public int DelayMs { get; set; } = 1000;
}
```

### Register the Executor

In `Program.cs`:

```csharp
// Register built-in executors
builder.Services.AddTaskExecutor<DemoTaskExecutor>();
builder.Services.AddTaskExecutor<MyCustomTaskExecutor>();  // Add this line

// Or load from plugins folder
builder.Services.AddTaskExecutorPlugins(Path.Combine(AppContext.BaseDirectory, "plugins"));
```

### Frontend: Task Form Component

Create a form component extending `BaseTaskFormComponent`:

```typescript
import { Component } from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSliderModule } from '@angular/material/slider';
import { BaseTaskFormComponent } from '../base-task-form.component';

@Component({
  selector: 'app-my-custom-form',
  standalone: true,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSliderModule],
  template: `
    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Number of Steps</mat-label>
      <input matInput type="number" [formControl]="stepsControl">
      <mat-error *ngIf="stepsControl.hasError('required')">Steps is required</mat-error>
      <mat-error *ngIf="stepsControl.hasError('min')">Minimum 1 step</mat-error>
    </mat-form-field>

    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Custom Setting</mat-label>
      <input matInput [formControl]="customSettingControl">
      <mat-hint>Optional configuration value</mat-hint>
    </mat-form-field>

    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Delay per Step (ms)</mat-label>
      <input matInput type="number" [formControl]="delayControl">
    </mat-form-field>
  `,
  styles: [`
    .full-width { width: 100%; margin-bottom: 16px; }
  `]
})
export class MyCustomFormComponent extends BaseTaskFormComponent {
  stepsControl = new FormControl(5, [Validators.required, Validators.min(1), Validators.max(100)]);
  customSettingControl = new FormControl('');
  delayControl = new FormControl(1000, [Validators.required, Validators.min(100)]);

  form = new FormGroup({
    steps: this.stepsControl,
    customSetting: this.customSettingControl,
    delayMs: this.delayControl
  });

  getPayload(): object {
    return {
      steps: this.stepsControl.value,
      customSetting: this.customSettingControl.value,
      delayMs: this.delayControl.value
    };
  }
}
```

### Register the Form

In `TaskFormRegistryService`:

```typescript
import { Injectable } from '@angular/core';
import { MyCustomFormComponent } from './forms/my-custom-form.component';

@Injectable({ providedIn: 'root' })
export class TaskFormRegistryService {
  private registry = new Map<string, TaskFormConfig>();

  constructor() {
    // Register built-in forms
    this.register({
      taskType: 'countdown',
      displayName: 'Countdown Timer',
      component: CountdownFormComponent
    });

    // Register your custom form
    this.register({
      taskType: 'my-custom-task',
      displayName: 'My Custom Task',
      component: MyCustomFormComponent
    });
  }

  register(config: TaskFormConfig): void {
    this.registry.set(config.taskType, config);
  }

  get(taskType: string): TaskFormConfig | undefined {
    return this.registry.get(taskType);
  }

  getAll(): TaskFormConfig[] {
    return Array.from(this.registry.values());
  }
}

export interface TaskFormConfig {
  taskType: string;
  displayName: string;
  component: Type<BaseTaskFormComponent>;
}
```

---

## Auth Token Propagation for Microservice Calls

Tasks can make authenticated HTTP calls to downstream microservices using the JWT token from the original API request.

### How It Works

1. **Token Capture**: JWT token is captured at task creation and stored in `TaskItem.AuthToken`
2. **Token Access**: Executors access the token via `task.AuthToken`
3. **HttpClient Factory**: Use `ITaskHttpClientFactory` to create pre-configured HttpClient

### Example: Authenticated Executor

```csharp
public class MyMicroserviceExecutor : ITaskExecutor
{
    private readonly ITaskHttpClientFactory _httpClientFactory;

    public MyMicroserviceExecutor(ITaskHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string TaskType => "my-microservice-task";

    public async Task ExecuteAsync(TaskItem task, IProgress<TaskProgressUpdate> progress, CancellationToken ct)
    {
        // Create HttpClient with the user's auth token
        using var client = _httpClientFactory.CreateClient(task.AuthToken, "https://api.example.com");

        // All requests include: Authorization: Bearer <token>
        var response = await client.GetAsync("/api/data", ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        progress.Report(new TaskProgressUpdate(100, "Completed", content));
    }
}
```

### Microservice Call Plugin Task

The `microservice-call` task type is a plugin for making HTTP calls with auth propagation:

```json
{
  "type": "microservice-call",
  "payload": "{\"url\":\"https://api.example.com/endpoint\",\"method\":\"GET\",\"retryCount\":3,\"timeoutSeconds\":30}"
}
```

**Payload Options:**
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `url` | string | (required) | Target URL |
| `method` | string | GET | HTTP method |
| `requestBody` | string | null | Request body (for POST/PUT) |
| `contentType` | string | application/json | Content type |
| `retryCount` | int | 0 | Retry attempts on failure |
| `retryDelayMs` | int | 1000 | Delay between retries |
| `timeoutSeconds` | int | 30 | Request timeout |

---

## JSON Payloads for Hierarchical Tasks

### Simple Parent with Mixed Children

```json
{
  "parentTask": {
    "type": "hierarchical-parent",
    "priority": 5,
    "payload": "{\"parentName\":\"Data Pipeline\"}",
    "subtaskParallelism": false
  },
  "childTasks": [
    {
      "parentTask": {
        "type": "countdown",
        "priority": 5,
        "payload": "{\"durationInSeconds\":10}",
        "weight": 1.0
      },
      "childTasks": []
    },
    {
      "parentTask": {
        "type": "rolldice",
        "priority": 5,
        "payload": "{\"desiredDice1\":6,\"desiredDice2\":6}",
        "weight": 2.0
      },
      "childTasks": []
    }
  ]
}
```

### Multi-Level Nested Hierarchy

```json
{
  "parentTask": {
    "type": "hierarchical-parent",
    "priority": 5,
    "payload": "{\"parentName\":\"Complex ETL Workflow\"}",
    "subtaskParallelism": false
  },
  "childTasks": [
    {
      "parentTask": {
        "type": "countdown",
        "priority": 5,
        "payload": "{\"durationInSeconds\":5}",
        "weight": 1.0
      },
      "childTasks": []
    },
    {
      "parentTask": {
        "type": "hierarchical-parent",
        "priority": 5,
        "payload": "{\"parentName\":\"Parallel Processing Phase\"}",
        "subtaskParallelism": true
      },
      "childTasks": [
        {
          "parentTask": {
            "type": "demo",
            "priority": 5,
            "payload": "{\"durationSeconds\":3,\"steps\":3}",
            "weight": 1.0
          },
          "childTasks": []
        },
        {
          "parentTask": {
            "type": "demo",
            "priority": 5,
            "payload": "{\"durationSeconds\":4,\"steps\":4}",
            "weight": 1.5
          },
          "childTasks": []
        },
        {
          "parentTask": {
            "type": "rolldice",
            "priority": 5,
            "payload": "{\"desiredDice1\":3,\"desiredDice2\":3}",
            "weight": 2.0
          },
          "childTasks": []
        }
      ]
    },
    {
      "parentTask": {
        "type": "countdown",
        "priority": 5,
        "payload": "{\"durationInSeconds\":3}",
        "weight": 0.5
      },
      "childTasks": []
    }
  ]
}
```

### Using Different Groups for Children

```json
{
  "parentTask": {
    "type": "hierarchical-parent",
    "priority": 5,
    "payload": "{\"parentName\":\"Multi-Group Workflow\"}",
    "subtaskParallelism": true
  },
  "childTasks": [
    {
      "parentTask": {
        "type": "countdown",
        "priority": 5,
        "payload": "{\"durationInSeconds\":10}",
        "groupId": "00000000-0000-0000-0000-000000000001",
        "weight": 1.0
      },
      "childTasks": []
    },
    {
      "parentTask": {
        "type": "demo",
        "priority": 5,
        "payload": "{\"durationSeconds\":5,\"steps\":5}",
        "groupId": "00000000-0000-0000-0000-000000000002",
        "weight": 1.0
      },
      "childTasks": []
    }
  ]
}
```

---

## Configuration Reference

### Backend Settings (`appsettings.json`)

```json
{
  "TaskServer": {
    "DefaultTaskTimeoutMinutes": 60,
    "TaskQueuePollingIntervalMs": 100
  },
  "SignalR": {
    "EnableDetailedErrors": false,
    "KeepAliveIntervalSeconds": 15,
    "ClientTimeoutSeconds": 30
  },
  "Authentication": {
    "Firebase": {
      "ProjectId": "your-project-id",
      "ValidAudience": "your-project-id",
      "ValidIssuer": "https://securetoken.google.com/your-project-id"
    }
  }
}
```

### Default Task Groups

| Group Name | Max Parallelism | Use Case |
|------------|-----------------|----------|
| `default` | 32 | General purpose tasks |
| `cpu-processing` | CPU core count | CPU-bound operations |
| `exclusive-processing` | 1 | Sequential execution (e.g., radio transmission) |

### Frontend Environment (`environment.ts`)

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5128',
  signalRUrl: 'http://localhost:5128'
};
```

---

## Project Structure

```
simple-task-server/
├── backend/task-server/
│   ├── src/
│   │   ├── TaskServer.Api/           # REST API + SignalR hub
│   │   ├── TaskServer.Core/          # Domain models, interfaces, DTOs
│   │   ├── TaskServer.Infrastructure/ # Service implementations
│   │   └── plugins/                  # Task executor plugins
│   └── tests/
├── frontend/task-manager/
│   └── src/app/
│       ├── core/                     # Services, models
│       ├── features/tasks/           # Task components
│       └── shared/task-forms/        # Form components
└── README.md
```

---

## Task States

```
Queued → Executing → Completed
                  → Cancelled
                  → Errored
                  → Terminated (timeout)
```

---

## License

MIT License
