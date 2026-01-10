# Pulstrade - Angular Frontend

Angular 21 SPA for task management with real-time progress tracking via SignalR. Features hierarchical task visualization with tree view, expand/collapse all, real-time history tracking, connection status indicator, and support for mixed task types.

## Quick Start

```bash
# Install dependencies
npm install

# Start development server (http://localhost:4200)
npm start

# Build for production
npm run build

# Run tests
npm test
```

Requires backend running on http://localhost:5128 (proxied automatically in development).

## Architecture

```
src/app/
├── core/
│   ├── models/           # TaskResponse, TaskGroup, TaskTreeNode, history interfaces
│   ├── services/         # API, store, and SignalR services
│   └── components/       # ConnectionStatusComponent
├── features/tasks/
│   ├── components/       # TaskTree, TaskTreeItem, TaskProgress, TaskStateBadge
│   ├── dialogs/          # CreateTaskDialog
│   └── pages/            # TasksPage
└── shared/task-forms/    # BaseTaskFormComponent, TaskFormRegistry, form implementations
```

### Key Services

| Service | Description |
|---------|-------------|
| `TaskApiService` | REST API client for task CRUD operations |
| `TaskStoreService` | Reactive state management with BehaviorSubject/Map, history tracking |
| `TaskGroupStoreService` | Group management and state |
| `SignalRService` | Real-time WebSocket connection with resilient reconnection |

### State Management

`TaskStoreService` uses reactive patterns:
- `tasksMap$`: BehaviorSubject<Map<string, TaskResponse>> - flat task storage
- `taskTree$`: Observable<TaskTreeNode[]> - hierarchical tree built from flat map
- `expandedTaskIds$`: BehaviorSubject<Set<string>> - expand/collapse state
- `progressHistoryMap$`: BehaviorSubject<Map<string, ProgressHistoryEntry[]>> - child progress history
- `stateHistoryMap$`: BehaviorSubject<Map<string, StateChangeHistoryEntry[]>> - child state changes

## Features

### Hierarchical Task System

**Tree View Features:**
- **Expand/Collapse All**: Toggle visibility of all nested tasks at any depth
- **Per-Item Expand**: Click chevron to expand/collapse individual items
- **Nested Hierarchy**: Supports unlimited nesting depth
- **Mixed Task Types**: Each child can be any registered task type (countdown, rolldice, hierarchical-parent, etc.)

**Progress Display:**
- Parent tasks show aggregated progress (weighted average of children)
- Each task displays its own type, status, and progress independently
- Progress bars update in real-time via SignalR
- Weight-based aggregation: `Parent Progress = Σ(child.progress × child.weight) / Σ(child.weight)`

### Real-time History Tracking

Root hierarchical tasks display live history boxes:

**Progress History Box:**
- Shows last 20 child progress updates (scrollable, 3 lines visible)
- Format: `HH:mm:ss taskType displayText` (e.g., `15:18:54 rolldice Rolled 6-6!`)
- Excludes aggregated parent progress (only shows actual child updates)

**State Change History Box:**
- Shows last 20 child state transitions
- Format: `HH:mm:ss taskIdShort... taskType newState`
- Color-coded states (green=Completed, blue=Executing, red=Errored)

### SignalR Connection Resilience

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

### UI Enhancements

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

## Creating Hierarchical Tasks

### Via Create Task Dialog

1. Select "Simple Hierarchical" task type
2. Configure parent task settings (name, subtask parallelism)
3. Add child tasks using "Add Child Task" button
4. Each child can have:
   - **Type**: Any registered task type (countdown, rolldice, generic, or another hierarchical)
   - **Weight**: Progress contribution weight (default 1.0)
   - Children can be added to create nested hierarchies

### Child Task Types

When adding children, select from:
- **Countdown**: Timer task with configurable duration
- **Roll Dice**: Random dice rolling until target combination
- **Generic**: Custom type with JSON payload
- **Simple Hierarchical**: Nested parent with its own children

## Task Forms

### Extending with Custom Forms

1. Create component extending `BaseTaskFormComponent`:

```typescript
@Component({...})
export class MyTaskFormComponent extends BaseTaskFormComponent {
  form = new FormGroup({
    myField: new FormControl('', Validators.required)
  });

  getPayload(): object {
    return this.form.value;
  }

  isValid(): boolean {
    return this.form.valid;
  }
}
```

2. Register in `TaskFormRegistryService`:

```typescript
this.registry.register({
  taskType: 'my-task',
  displayName: 'My Task',
  component: MyTaskFormComponent
});
```

### Built-in Forms

| Task Type | Form Component | Description |
|-----------|---------------|-------------|
| `countdown` | CountdownFormComponent | Duration input with validation |
| `rolldice` | RollDiceFormComponent | Dice selection with odds calculator |
| `generic` | GenericFormComponent | Manual type + JSON payload |
| `simple-hierarchical` | SimpleHierarchicalFormComponent | Hierarchical parent with child configuration |

## SignalR Events

### Subscribed Events

| Event | Description |
|-------|-------------|
| `OnTaskCreated` | New task added - updates store |
| `OnTaskUpdated` | Task modified - updates store |
| `OnTaskDeleted` | Task removed - removes from store |
| `OnStateChanged` | Task state transition - updates store, adds to state history |
| `OnProgress` | Progress update with percentage, details, payload - adds to progress history |

### Connection Management

- Auto-reconnect on disconnect (automatic + manual fallback)
- Subscribes to all user tasks on connect
- Connection status displayed in toolbar
- Manual reconnect available via click

## Components

### TaskTreeComponent

Main tree view container with:
- Expand All / Collapse All buttons
- Recursive rendering of TaskTreeItemComponent
- Real-time updates from store

### TaskTreeItemComponent

Individual tree node with:
- Expand/collapse chevron for parents
- Task type badge and status indicator
- Progress bar with percentage
- Action buttons (cancel, delete) - auto-subtree for hierarchical tasks
- Indentation based on depth
- Background color by nesting level
- Progress history box (root hierarchical only)
- State change history box (root hierarchical only)

### TaskProgressComponent

Progress visualization with:
- Percentage bar
- Details text
- Progress payload display (JSON data)

### ConnectionStatusComponent

Connection indicator with:
- Visual status (green/yellow/red dot)
- Icon (wifi/sync/wifi_off)
- Tooltip with connection details
- Click to manually reconnect

### CreateTaskDialogComponent

Task creation dialog with:
- Task type selection dropdown
- Priority and group selection
- Dynamic form loading via TaskFormHostComponent
- Scrollable content with sticky footer
- Hierarchical task creation with unlimited nesting

## Configuration

### Environment Variables

```typescript
// environment.ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5128',
  signalRUrl: 'http://localhost:5128'
};
```

### Proxy Configuration

Development proxy (`proxy.conf.json`):
```json
{
  "/api": {
    "target": "http://localhost:5128",
    "secure": false
  },
  "/hubs": {
    "target": "http://localhost:5128",
    "secure": false,
    "ws": true
  }
}
```

## Development Commands

```bash
# Start dev server with live reload
npm start

# Build production bundle
npm run build

# Run unit tests (Vitest)
npm test

# Generate new component
ng generate component features/tasks/components/my-component

# Type checking
npm run build -- --configuration=development
```

## Project Dependencies

- **Angular 21**: Core framework
- **Angular Material**: UI components (buttons, dialogs, forms, menus)
- **@microsoft/signalr**: Real-time WebSocket client
- **RxJS**: Reactive state management

## Troubleshooting

### Tasks not appearing
- Verify backend is running on port 5128
- Check SignalR connection status indicator
- Look for console errors

### SignalR not connecting
- Ensure backend is running
- Check proxy configuration
- Verify WebSocket support
- Connection status indicator shows details

### Expand All not working for nested tasks
- Both `expandAll()` in store and `hasChildren` in component must use actual data relationships
- The fix checks `parentTaskId` references instead of `childCount` property

### Create dialog buttons overlaying form
- Dialog uses scrollable content with sticky footer
- `max-height: 80vh` on host, `overflow-y: auto` on content
- Actions pinned with `position: sticky; bottom: 0`

### History boxes not showing
- Only shown for root hierarchical tasks (level 0)
- Requires children to be executing and reporting progress
- Check that task has `hasChildren` (children in tree or childCount > 0)
