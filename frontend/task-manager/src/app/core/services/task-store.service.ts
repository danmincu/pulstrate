import { Injectable, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, Observable, map, tap, catchError, of, combineLatest } from 'rxjs';
import { TaskApiService } from './task-api.service';
import { SignalRService } from './signalr.service';
import { CreateTaskRequest, CreateTaskHierarchyRequest, TaskResponse, TaskTreeNode, ProgressHistoryEntry, StateChangeHistoryEntry, TaskState } from '../models/task.model';

@Injectable({ providedIn: 'root' })
export class TaskStoreService {
  private readonly taskApi = inject(TaskApiService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly tasksMap$ = new BehaviorSubject<Map<string, TaskResponse>>(new Map());
  private readonly loadingSubject$ = new BehaviorSubject<boolean>(false);
  private readonly errorSubject$ = new BehaviorSubject<string | null>(null);
  private readonly expandedTaskIds$ = new BehaviorSubject<Set<string>>(new Set());

  // History tracking maps - keyed by ROOT parent task ID (or task's own ID if it's a root)
  private readonly progressHistoryMap$ = new BehaviorSubject<Map<string, ProgressHistoryEntry[]>>(new Map());
  private readonly stateHistoryMap$ = new BehaviorSubject<Map<string, StateChangeHistoryEntry[]>>(new Map());
  private static readonly MAX_PROGRESS_HISTORY = 100;
  private static readonly MAX_STATE_HISTORY = 50;

  readonly tasks$: Observable<TaskResponse[]> = this.tasksMap$.pipe(
    map(taskMap => Array.from(taskMap.values())),
    map(tasks => tasks.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()))
  );

  /** Tree view of tasks - only root tasks (no parent) at top level */
  readonly taskTree$: Observable<TaskTreeNode[]> = combineLatest([
    this.tasksMap$,
    this.expandedTaskIds$,
    this.progressHistoryMap$,
    this.stateHistoryMap$
  ]).pipe(
    map(([taskMap, expandedIds, progressHistory, stateHistory]) =>
      this.buildTaskTree(taskMap, expandedIds, progressHistory, stateHistory))
  );

  readonly loading$ = this.loadingSubject$.asObservable();
  readonly error$ = this.errorSubject$.asObservable();
  readonly expandedIds$ = this.expandedTaskIds$.asObservable();

  constructor() {
    this.subscribeToSignalREvents();
  }

  loadTasks(): void {
    this.loadingSubject$.next(true);
    this.errorSubject$.next(null);

    this.taskApi.getTasks().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(tasks => {
        const taskMap = new Map<string, TaskResponse>();
        tasks.forEach(t => taskMap.set(t.id, t));
        this.tasksMap$.next(taskMap);
        this.loadingSubject$.next(false);
      }),
      catchError(err => {
        console.error('Failed to load tasks:', err);
        this.errorSubject$.next('Failed to load tasks');
        this.loadingSubject$.next(false);
        return of([]);
      })
    ).subscribe();
  }

  createTask(request: CreateTaskRequest): Observable<TaskResponse> {
    return this.taskApi.createTask(request).pipe(
      tap(task => {
        this.updateTask(task);
      }),
      catchError(err => {
        console.error('Failed to create task:', err);
        this.errorSubject$.next('Failed to create task');
        throw err;
      })
    );
  }

  cancelTask(id: string): Observable<TaskResponse> {
    return this.taskApi.cancelTask(id).pipe(
      tap(task => {
        this.updateTask(task);
      }),
      catchError(err => {
        console.error('Failed to cancel task:', err);
        this.errorSubject$.next('Failed to cancel task');
        throw err;
      })
    );
  }

  deleteTask(id: string): Observable<void> {
    return this.taskApi.deleteTask(id).pipe(
      tap(() => {
        this.removeTask(id);
      }),
      catchError(err => {
        console.error('Failed to delete task:', err);
        this.errorSubject$.next('Failed to delete task');
        throw err;
      })
    );
  }

  getTaskById(id: string): Observable<TaskResponse | undefined> {
    return this.tasksMap$.pipe(
      map(taskMap => taskMap.get(id))
    );
  }

  // Hierarchical task methods

  createTaskHierarchy(request: CreateTaskHierarchyRequest): Observable<TaskResponse> {
    return this.taskApi.createTaskHierarchy(request).pipe(
      tap(task => {
        this.updateTask(task);
      }),
      catchError(err => {
        console.error('Failed to create task hierarchy:', err);
        this.errorSubject$.next('Failed to create task hierarchy');
        throw err;
      })
    );
  }

  cancelTaskSubtree(id: string): Observable<TaskResponse> {
    return this.taskApi.cancelTaskSubtree(id).pipe(
      tap(task => {
        this.updateTask(task);
      }),
      catchError(err => {
        console.error('Failed to cancel task subtree:', err);
        this.errorSubject$.next('Failed to cancel task subtree');
        throw err;
      })
    );
  }

  deleteTaskSubtree(id: string): Observable<void> {
    return this.taskApi.deleteTaskSubtree(id).pipe(
      tap(() => {
        // Remove all descendants from the store
        const tasks = this.tasksMap$.value;
        const toRemove = this.getDescendantIds(id, tasks);
        toRemove.push(id);
        const newTasks = new Map(tasks);
        toRemove.forEach(taskId => newTasks.delete(taskId));
        this.tasksMap$.next(newTasks);
      }),
      catchError(err => {
        console.error('Failed to delete task subtree:', err);
        this.errorSubject$.next('Failed to delete task subtree');
        throw err;
      })
    );
  }

  // Tree expansion state management

  toggleExpanded(taskId: string): void {
    const expanded = new Set(this.expandedTaskIds$.value);
    if (expanded.has(taskId)) {
      expanded.delete(taskId);
    } else {
      expanded.add(taskId);
    }
    this.expandedTaskIds$.next(expanded);
  }

  expandAll(): void {
    const tasks = this.tasksMap$.value;
    // Find all task IDs that are referenced as parentTaskId by other tasks
    // This is more reliable than relying on childCount which may not be set for nested tasks
    const parentsWithChildren = new Set<string>();
    tasks.forEach(task => {
      if (task.parentTaskId) {
        parentsWithChildren.add(task.parentTaskId);
      }
    });
    this.expandedTaskIds$.next(parentsWithChildren);
  }

  collapseAll(): void {
    this.expandedTaskIds$.next(new Set());
  }

  isExpanded(taskId: string): boolean {
    return this.expandedTaskIds$.value.has(taskId);
  }

  private subscribeToSignalREvents(): void {
    this.signalR.taskCreated$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(task => {
      this.updateTask(task);
    });

    this.signalR.taskUpdated$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(task => {
      this.updateTask(task);
    });

    this.signalR.taskDeleted$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(taskId => {
      this.removeTask(taskId);
    });

    this.signalR.stateChanged$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ taskId, newState, details }) => {
      this.patchTask(taskId, { state: newState as TaskState, stateDetails: details });
      this.addStateChangeHistory(taskId, newState as TaskState);
    });

    this.signalR.progress$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(({ taskId, percentage, details, payload }) => {
      this.patchTask(taskId, { progress: percentage, progressDetails: details, progressPayload: payload });
      // Only track progress history if there's meaningful content (not aggregated parent progress)
      if (details && !details.startsWith('Aggregated from')) {
        this.addProgressHistory(taskId, percentage, details, payload);
      }
    });
  }

  private pendingPatches = new Map<string, Partial<TaskResponse>[]>();

  private patchTask(taskId: string, patch: Partial<TaskResponse>): void {
    const tasks = new Map(this.tasksMap$.value);
    const existing = tasks.get(taskId);
    if (existing) {
      // Apply patch to existing task
      tasks.set(taskId, { ...existing, ...patch });
      this.tasksMap$.next(tasks);
    } else {
      // Task not yet in store - queue the patch for later
      const patches = this.pendingPatches.get(taskId) || [];
      patches.push(patch);
      this.pendingPatches.set(taskId, patches);
    }
  }

  private updateTask(task: TaskResponse): void {
    const tasks = new Map(this.tasksMap$.value);

    // Apply any pending patches to the task
    const patches = this.pendingPatches.get(task.id);
    if (patches && patches.length > 0) {
      let patchedTask = task;
      for (const patch of patches) {
        patchedTask = { ...patchedTask, ...patch };
      }
      tasks.set(task.id, patchedTask);
      this.pendingPatches.delete(task.id);
    } else {
      tasks.set(task.id, task);
    }

    this.tasksMap$.next(tasks);
  }

  private removeTask(taskId: string): void {
    const tasks = new Map(this.tasksMap$.value);
    tasks.delete(taskId);
    this.tasksMap$.next(tasks);
  }

  // History tracking helpers

  private findRootTaskId(taskId: string): string {
    const tasks = this.tasksMap$.value;
    let currentTask = tasks.get(taskId);

    // Traverse up the parent chain to find the root
    while (currentTask?.parentTaskId) {
      const parent = tasks.get(currentTask.parentTaskId);
      if (!parent) break;
      currentTask = parent;
    }

    return currentTask?.id ?? taskId;
  }

  private addProgressHistory(taskId: string, percentage: number, details: string | null, payload: string | null): void {
    const tasks = this.tasksMap$.value;
    const task = tasks.get(taskId);
    if (!task) return;

    const rootId = this.findRootTaskId(taskId);

    // Extract display text from details (more human-readable)
    const displayText = details ?? `${percentage.toFixed(0)}%`;

    const entry: ProgressHistoryEntry = {
      taskId,
      taskType: task.type,
      timestamp: new Date(),
      percentage,
      details,
      payload,
      displayText
    };

    const historyMap = new Map(this.progressHistoryMap$.value);
    const history = historyMap.get(rootId) ?? [];
    history.push(entry);

    // Limit history size
    if (history.length > TaskStoreService.MAX_PROGRESS_HISTORY) {
      history.shift();
    }

    historyMap.set(rootId, history);
    this.progressHistoryMap$.next(historyMap);
  }

  private addStateChangeHistory(taskId: string, newState: TaskState): void {
    const tasks = this.tasksMap$.value;
    const task = tasks.get(taskId);
    if (!task) return;

    const rootId = this.findRootTaskId(taskId);

    const entry: StateChangeHistoryEntry = {
      taskId,
      taskType: task.type,
      taskIdShort: taskId.substring(0, 8),
      timestamp: new Date(),
      newState
    };

    const historyMap = new Map(this.stateHistoryMap$.value);
    const history = historyMap.get(rootId) ?? [];
    history.push(entry);

    // Limit history size
    if (history.length > TaskStoreService.MAX_STATE_HISTORY) {
      history.shift();
    }

    historyMap.set(rootId, history);
    this.stateHistoryMap$.next(historyMap);
  }

  // Tree building helpers

  private buildTaskTree(
    taskMap: Map<string, TaskResponse>,
    expandedIds: Set<string>,
    progressHistoryMap: Map<string, ProgressHistoryEntry[]>,
    stateHistoryMap: Map<string, StateChangeHistoryEntry[]>
  ): TaskTreeNode[] {
    const tasks = Array.from(taskMap.values());

    // Build a map of parent -> children (use 'ROOT' for null/undefined parents)
    const childrenMap = new Map<string, TaskResponse[]>();
    const ROOT_KEY = '__ROOT__';

    tasks.forEach(task => {
      const parentId = task.parentTaskId ?? ROOT_KEY;
      if (!childrenMap.has(parentId)) {
        childrenMap.set(parentId, []);
      }
      childrenMap.get(parentId)!.push(task);
    });

    // Recursively build tree nodes starting from root (parentId = null/undefined)
    const buildNode = (task: TaskResponse, level: number, rootId: string): TaskTreeNode => {
      const children = childrenMap.get(task.id) || [];
      const childNodes = children
        .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
        .map(child => buildNode(child, level + 1, rootId));

      // Only root level (level 0) gets the history arrays populated
      const progressHistory = level === 0 ? (progressHistoryMap.get(task.id) ?? []) : [];
      const stateChangeHistory = level === 0 ? (stateHistoryMap.get(task.id) ?? []) : [];

      return {
        task,
        children: childNodes,
        level,
        expanded: expandedIds.has(task.id),
        aggregatedProgress: this.calculateAggregatedProgress(task, childNodes),
        progressHistory,
        stateChangeHistory
      };
    };

    // Get root tasks (no parent) and build tree
    const rootTasks = childrenMap.get(ROOT_KEY) || [];
    return rootTasks
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .map(task => buildNode(task, 0, task.id));
  }

  private calculateAggregatedProgress(task: TaskResponse, childNodes: TaskTreeNode[]): number {
    if (childNodes.length === 0) {
      return task.progress;
    }

    // Calculate weighted average of immediate children
    const totalWeight = childNodes.reduce((sum, node) => sum + node.task.weight, 0);
    if (totalWeight === 0) return 0;

    const weightedSum = childNodes.reduce((sum, node) => {
      return sum + (node.task.weight / totalWeight) * node.task.progress;
    }, 0);

    return weightedSum;
  }

  private getDescendantIds(taskId: string, tasks: Map<string, TaskResponse>): string[] {
    const descendants: string[] = [];
    const queue: string[] = [taskId];

    while (queue.length > 0) {
      const currentId = queue.shift()!;
      tasks.forEach(task => {
        if (task.parentTaskId === currentId) {
          descendants.push(task.id);
          queue.push(task.id);
        }
      });
    }

    return descendants;
  }
}
