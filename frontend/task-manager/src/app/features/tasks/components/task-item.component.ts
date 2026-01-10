import { Component, Input, Output, EventEmitter } from '@angular/core';
import { DatePipe, TitleCasePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TaskResponse, isTerminalState } from '../../../core/models/task.model';
import { TaskStateBadgeComponent } from './task-state-badge.component';
import { TaskProgressComponent } from './task-progress.component';

@Component({
  selector: 'app-task-item',
  standalone: true,
  imports: [
    DatePipe,
    TitleCasePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    TaskStateBadgeComponent,
    TaskProgressComponent
  ],
  template: `
    <mat-card class="task-card" [class.terminal]="isTerminal">
      <mat-card-header>
        <mat-card-title>{{ task.type | titlecase }}</mat-card-title>
        <mat-card-subtitle class="task-id">{{ task.id }}</mat-card-subtitle>
        <div class="badge-container">
          <app-task-state-badge [state]="task.state"></app-task-state-badge>
          <span class="group-badge">{{ task.groupName }}</span>
        </div>
      </mat-card-header>

      <mat-card-content>
        <app-task-progress
          [progress]="task.progress"
          [details]="task.progressDetails"
          [state]="task.state">
        </app-task-progress>

        @if (task.stateDetails) {
          <p class="state-details">{{ task.stateDetails }}</p>
        }

        <div class="timestamps">
          <span>Created: {{ task.createdAt | date:'short' }}</span>
          @if (task.startedAt) {
            <span>Started: {{ task.startedAt | date:'short' }}</span>
          }
          @if (task.completedAt) {
            <span>Completed: {{ task.completedAt | date:'short' }}</span>
          }
        </div>
      </mat-card-content>

      <mat-card-actions>
        @if (!isTerminal) {
          <button mat-button color="warn" (click)="cancel.emit()">
            <mat-icon>cancel</mat-icon>
            <span>Cancel</span>
          </button>
        }
        @if (isTerminal) {
          <button mat-button color="warn" (click)="delete.emit()">
            <mat-icon>delete</mat-icon>
            <span>Delete</span>
          </button>
        }
      </mat-card-actions>
    </mat-card>
  `,
  styles: [`
    .task-card {
      margin-bottom: 16px;
    }

    .task-card.terminal {
      opacity: 0.8;
    }

    mat-card-header {
      position: relative;
    }

    .task-id {
      font-family: monospace;
      font-size: 12px;
    }

    .badge-container {
      position: absolute;
      top: 16px;
      right: 16px;
      display: flex;
      gap: 8px;
      align-items: center;
    }

    .group-badge {
      font-size: 11px;
      padding: 2px 8px;
      border-radius: 12px;
      background-color: #e3f2fd;
      color: #1565c0;
      font-weight: 500;
    }

    .state-details {
      color: rgba(0, 0, 0, 0.6);
      font-style: italic;
      margin: 8px 0;
    }

    .timestamps {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      font-size: 12px;
      color: rgba(0, 0, 0, 0.54);
      margin-top: 8px;
    }

    mat-card-actions {
      padding: 8px 16px;
    }
  `]
})
export class TaskItemComponent {
  @Input() task!: TaskResponse;
  @Output() cancel = new EventEmitter<void>();
  @Output() delete = new EventEmitter<void>();

  get isTerminal(): boolean {
    return isTerminalState(this.task.state);
  }
}
