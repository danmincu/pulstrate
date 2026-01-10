import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { TaskResponse, TaskState } from '../models/task.model';

export interface StateChangedEvent {
  taskId: string;
  newState: TaskState;
  details: string | null;
}

export interface ProgressEvent {
  taskId: string;
  percentage: number;
  details: string | null;
  payload: string | null;
}

export type ConnectionStatus = 'connected' | 'connecting' | 'reconnecting' | 'disconnected';

export interface ConnectionInfo {
  status: ConnectionStatus;
  lastError: string | null;
  reconnectAttempt: number;
  lastConnectedAt: Date | null;
  lastDisconnectedAt: Date | null;
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hubConnection: HubConnection | null = null;
  private manualReconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private reconnectAttempt = 0;
  private lastError: string | null = null;
  private lastConnectedAt: Date | null = null;
  private lastDisconnectedAt: Date | null = null;
  private isManuallyDisconnected = false;

  // Reconnection configuration
  private readonly maxManualReconnectAttempts = 10;
  private readonly manualReconnectDelays = [1000, 2000, 5000, 10000, 15000, 30000, 60000];

  readonly connectionState$ = new BehaviorSubject<HubConnectionState>(HubConnectionState.Disconnected);
  readonly connectionInfo$ = new BehaviorSubject<ConnectionInfo>(this.buildConnectionInfo('disconnected'));

  // Event subjects
  readonly taskCreated$ = new Subject<TaskResponse>();
  readonly taskUpdated$ = new Subject<TaskResponse>();
  readonly taskDeleted$ = new Subject<string>();
  readonly stateChanged$ = new Subject<StateChangedEvent>();
  readonly progress$ = new Subject<ProgressEvent>();

  // Emitted when connection is restored after being disconnected
  readonly reconnected$ = new Subject<void>();

  async connect(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return;
    }

    this.isManuallyDisconnected = false;
    this.cancelManualReconnect();

    if (!this.hubConnection) {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl(`${environment.signalRUrl}/hubs/tasks`)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Custom retry strategy: 0, 1s, 2s, 5s, 10s, 30s (max)
            const delays = [0, 1000, 2000, 5000, 10000, 30000];
            const delay = delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
            this.reconnectAttempt = retryContext.previousRetryCount + 1;
            this.updateConnectionInfo('reconnecting');
            console.log(`SignalR: Auto-reconnect attempt ${this.reconnectAttempt}, waiting ${delay}ms`);
            return delay;
          }
        })
        .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
        .build();

      this.setupEventHandlers();
      this.setupConnectionStateHandlers();
    }

    this.updateConnectionInfo('connecting');

    try {
      await this.hubConnection.start();
      this.onConnectionEstablished();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error';
      console.error('SignalR connection error:', errorMessage);
      this.lastError = errorMessage;
      this.updateConnectionInfo('disconnected');
      // Start manual reconnect attempts
      this.scheduleManualReconnect();
    }
  }

  async disconnect(): Promise<void> {
    this.isManuallyDisconnected = true;
    this.cancelManualReconnect();

    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
      this.lastDisconnectedAt = new Date();
      this.updateConnectionInfo('disconnected');
    }
  }

  /**
   * Manually trigger a reconnection attempt.
   * Useful when user wants to force a reconnect after prolonged disconnection.
   */
  async reconnect(): Promise<void> {
    console.log('SignalR: Manual reconnect requested');
    this.reconnectAttempt = 0;
    this.lastError = null;

    if (this.hubConnection) {
      // Stop existing connection first
      try {
        await this.hubConnection.stop();
      } catch {
        // Ignore stop errors
      }
      this.hubConnection = null;
    }

    await this.connect();
  }

  /**
   * Get current connection state
   */
  get isConnected(): boolean {
    return this.hubConnection?.state === HubConnectionState.Connected;
  }

  private onConnectionEstablished(): void {
    const wasDisconnected = this.lastConnectedAt !== null && this.reconnectAttempt > 0;

    this.reconnectAttempt = 0;
    this.lastError = null;
    this.lastConnectedAt = new Date();
    this.connectionState$.next(HubConnectionState.Connected);
    this.updateConnectionInfo('connected');
    console.log('SignalR connected');

    // Emit reconnected event if this was a reconnection (not initial connect)
    if (wasDisconnected) {
      console.log('SignalR: Reconnected after disconnection, emitting reconnected event');
      this.reconnected$.next();
    }
  }

  private setupEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('OnTaskCreated', (task: TaskResponse) => {
      console.log('SignalR: Task created', task.id, 'groupName:', task.groupName, 'groupId:', task.groupId);
      this.taskCreated$.next(task);
    });

    this.hubConnection.on('OnTaskUpdated', (task: TaskResponse) => {
      console.log('SignalR: Task updated', task.id);
      this.taskUpdated$.next(task);
    });

    this.hubConnection.on('OnTaskDeleted', (taskId: string) => {
      console.log('SignalR: Task deleted', taskId);
      this.taskDeleted$.next(taskId);
    });

    this.hubConnection.on('OnStateChanged', (taskId: string, newState: TaskState, details: string | null) => {
      console.log('SignalR: State changed', taskId, newState);
      this.stateChanged$.next({ taskId, newState, details });
    });

    this.hubConnection.on('OnProgress', (taskId: string, percentage: number, details: string | null, payload: string | null) => {
      console.log('SignalR: Progress', taskId, percentage, payload);
      this.progress$.next({ taskId, percentage, details, payload });
    });
  }

  private setupConnectionStateHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.onreconnecting((error) => {
      const errorMessage = error?.message ?? 'Connection lost';
      console.log('SignalR reconnecting...', errorMessage);
      this.lastError = errorMessage;
      this.connectionState$.next(HubConnectionState.Reconnecting);
      this.updateConnectionInfo('reconnecting');
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('SignalR reconnected with connectionId:', connectionId);
      this.onConnectionEstablished();
    });

    this.hubConnection.onclose((error) => {
      const errorMessage = error?.message ?? null;
      console.log('SignalR disconnected', errorMessage);
      this.lastError = errorMessage;
      this.lastDisconnectedAt = new Date();
      this.connectionState$.next(HubConnectionState.Disconnected);
      this.updateConnectionInfo('disconnected');

      // If not manually disconnected, start manual reconnect attempts
      // This handles the case where automatic reconnect gave up
      if (!this.isManuallyDisconnected) {
        console.log('SignalR: Automatic reconnect exhausted, starting manual reconnect');
        this.scheduleManualReconnect();
      }
    });
  }

  private scheduleManualReconnect(): void {
    if (this.isManuallyDisconnected) {
      return;
    }

    if (this.reconnectAttempt >= this.maxManualReconnectAttempts) {
      console.log(`SignalR: Max manual reconnect attempts (${this.maxManualReconnectAttempts}) reached`);
      this.lastError = 'Max reconnection attempts reached. Click to retry.';
      this.updateConnectionInfo('disconnected');
      return;
    }

    const delayIndex = Math.min(this.reconnectAttempt, this.manualReconnectDelays.length - 1);
    const delay = this.manualReconnectDelays[delayIndex];

    console.log(`SignalR: Scheduling manual reconnect attempt ${this.reconnectAttempt + 1} in ${delay}ms`);

    this.manualReconnectTimer = setTimeout(async () => {
      this.reconnectAttempt++;
      this.updateConnectionInfo('reconnecting');

      try {
        // Create fresh connection for manual reconnect
        this.hubConnection = null;
        await this.connect();
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Reconnection failed';
        console.error('SignalR manual reconnect failed:', errorMessage);
        this.lastError = errorMessage;
        this.updateConnectionInfo('disconnected');
        // Schedule another attempt
        this.scheduleManualReconnect();
      }
    }, delay);
  }

  private cancelManualReconnect(): void {
    if (this.manualReconnectTimer) {
      clearTimeout(this.manualReconnectTimer);
      this.manualReconnectTimer = null;
    }
  }

  private buildConnectionInfo(status: ConnectionStatus): ConnectionInfo {
    return {
      status,
      lastError: this.lastError,
      reconnectAttempt: this.reconnectAttempt,
      lastConnectedAt: this.lastConnectedAt,
      lastDisconnectedAt: this.lastDisconnectedAt
    };
  }

  private updateConnectionInfo(status: ConnectionStatus): void {
    this.connectionInfo$.next(this.buildConnectionInfo(status));
  }
}
