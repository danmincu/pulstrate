export type TaskState = 'Queued' | 'Executing' | 'Completed' | 'Cancelled' | 'Errored' | 'Terminated';

export interface TaskResponse {
  id: string;
  ownerId: string;
  groupId: string;
  groupName: string;
  priority: number;
  type: string;
  payload: string;
  state: TaskState;
  progress: number;
  progressDetails: string | null;
  progressPayload: string | null;
  stateDetails: string | null;
  createdAt: string;
  updatedAt: string;
  startedAt: string | null;
  completedAt: string | null;
  // Hierarchical task support
  parentTaskId: string | null;
  weight: number;
  subtaskParallelism: boolean;
  childCount: number;
  // History tracking
  trackHistory: boolean;
  progressHistory?: ApiProgressHistoryEntry[];
  stateChangeHistory?: ApiStateChangeHistoryEntry[];
}

// API DTOs for history (different from frontend display models)
export interface ApiProgressHistoryEntry {
  taskId: string;
  taskType: string;
  timestamp: string;
  percentage: number;
  details: string | null;
  payload: string | null;
}

export interface ApiStateChangeHistoryEntry {
  taskId: string;
  taskType: string;
  taskIdShort: string;
  timestamp: string;
  newState: string;
  details: string | null;
}

export interface CreateTaskRequest {
  id?: string;
  priority: number;
  type: string;
  payload: string;
  groupId?: string;
  // Hierarchical task support
  parentTaskId?: string;
  weight?: number;
  subtaskParallelism?: boolean;
  // History tracking
  trackHistory?: boolean;
}

export interface CreateTaskHierarchyRequest {
  parentTask: CreateTaskRequest;
  childTasks: CreateTaskHierarchyRequest[];
}

export interface ProgressHistoryEntry {
  taskId: string;
  taskType: string;
  timestamp: Date;
  percentage: number;
  details: string | null;
  payload: string | null;
  displayText: string;
}

export interface StateChangeHistoryEntry {
  taskId: string;
  taskType: string;
  taskIdShort: string;
  timestamp: Date;
  newState: TaskState;
}

export interface TaskTreeNode {
  task: TaskResponse;
  children: TaskTreeNode[];
  level: number;
  expanded: boolean;
  aggregatedProgress: number;
  // History from this task AND all descendants (populated for root hierarchical tasks)
  progressHistory: ProgressHistoryEntry[];
  stateChangeHistory: StateChangeHistoryEntry[];
}

export interface ErrorResponse {
  code: string;
  message: string;
  details?: string;
  traceId?: string;
}

export function isTerminalState(state: TaskState): boolean {
  return ['Completed', 'Cancelled', 'Errored', 'Terminated'].includes(state);
}

export function isActiveState(state: TaskState): boolean {
  return ['Queued', 'Executing'].includes(state);
}
