import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { TaskTreeComponent } from '../components/task-tree.component';
import { CreateTaskDialogComponent } from '../dialogs/create-task-dialog.component';
import { ConnectionStatusComponent } from '../../../core/components/connection-status.component';
import { TaskStoreService } from '../../../core/services/task-store.service';
import { SignalRService } from '../../../core/services/signalr.service';

@Component({
  selector: 'app-tasks-page',
  standalone: true,
  imports: [
    AsyncPipe,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    TaskTreeComponent,
    ConnectionStatusComponent
  ],
  template: `
    <mat-toolbar color="primary">
      <span>PULSTRATE</span>
      <span class="spacer"></span>
      <app-connection-status></app-connection-status>
      <button mat-raised-button class="new-task-btn" (click)="openCreateDialog()">
        <mat-icon>add</mat-icon>
        <span>New Task</span>
      </button>
    </mat-toolbar>

    <main class="content">
      <app-task-tree
        [nodes]="(taskTree$ | async) ?? []"
        [loading]="(loading$ | async) ?? false"
        (toggleExpand)="onToggleExpand($event)"
        (expandAll)="onExpandAll()"
        (collapseAll)="onCollapseAll()"
        (cancelTask)="onCancelTask($event)"
        (cancelSubtree)="onCancelSubtree($event)"
        (deleteTask)="onDeleteTask($event)"
        (deleteSubtree)="onDeleteSubtree($event)">
      </app-task-tree>
    </main>
    <div style="display: flex; align-items: center; gap: 8px; font-size: 14px; color: #4b5563;">
      <img
        src="https://lh3.googleusercontent.com/-Wjevx68ASu0/AAAAAAAAAAI/AAAAAAAAAAA/ALKGfklCx4YpJOqc_mLL47DDda3SqpWb5g/photo.jpg?sz=46"
        alt="Author profile"
        style="width: 24px; height: 24px; border-radius: 50%; object-fit: cover;">
      <span>© github.com/danmincu - 2026</span>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
    }

    mat-toolbar {
      position: sticky;
      top: 0;
      z-index: 100;
      gap: 8px;
    }

    .spacer {
      flex: 1 1 auto;
    }

    .content {
      flex: 1;
      overflow-y: auto;
      max-width: 800px;
      margin: 0 auto;
      width: 100%;
    }

    .new-task-btn {
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }

    app-connection-status {
      margin-right: 8px;
    }
  `]
})
export class TasksPageComponent implements OnInit, OnDestroy {
  private readonly taskStore = inject(TaskStoreService);
  private readonly signalR = inject(SignalRService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  taskTree$ = this.taskStore.taskTree$;
  loading$ = this.taskStore.loading$;

  constructor() {
    // Subscribe to reconnection events to refresh task list
    this.signalR.reconnected$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      console.log('Connection restored, refreshing task list...');
      this.taskStore.loadTasks();
    });
  }

  ngOnInit(): void {
    this.signalR.connect();
    this.taskStore.loadTasks();
  }

  ngOnDestroy(): void {
    this.signalR.disconnect();
  }

  openCreateDialog(): void {
    this.dialog.open(CreateTaskDialogComponent, {
      width: '500px',
      disableClose: true
    });
  }

  onToggleExpand(taskId: string): void {
    this.taskStore.toggleExpanded(taskId);
  }

  onExpandAll(): void {
    this.taskStore.expandAll();
  }

  onCollapseAll(): void {
    this.taskStore.collapseAll();
  }

  onCancelTask(taskId: string): void {
    this.taskStore.cancelTask(taskId).subscribe();
  }

  onCancelSubtree(taskId: string): void {
    this.taskStore.cancelTaskSubtree(taskId).subscribe();
  }

  onDeleteTask(taskId: string): void {
    this.taskStore.deleteTask(taskId).subscribe();
  }

  onDeleteSubtree(taskId: string): void {
    this.taskStore.deleteTaskSubtree(taskId).subscribe();
  }
}
