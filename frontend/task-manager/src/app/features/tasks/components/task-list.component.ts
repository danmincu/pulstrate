import { Component, Input, Output, EventEmitter } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TaskResponse } from '../../../core/models/task.model';
import { TaskItemComponent } from './task-item.component';

@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [
    MatCardModule,
    MatProgressSpinnerModule,
    TaskItemComponent
  ],
  template: `
    <div class="task-list">
      @if (loading) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (tasks.length === 0) {
        <mat-card class="empty-state">
          <mat-card-content>
            <p>No tasks yet. Create one to get started!</p>
          </mat-card-content>
        </mat-card>
      } @else {
        @for (task of tasks; track task.id) {
          <app-task-item
            [task]="task"
            (cancel)="cancelTask.emit(task.id)"
            (delete)="deleteTask.emit(task.id)">
          </app-task-item>
        }
      }
    </div>
  `,
  styles: [`
    .task-list {
      padding: 16px;
    }

    .loading-container {
      display: flex;
      justify-content: center;
      padding: 48px;
    }

    .empty-state {
      text-align: center;
      padding: 32px;
    }

    .empty-state p {
      color: rgba(0, 0, 0, 0.54);
      margin: 0;
    }
  `]
})
export class TaskListComponent {
  @Input() tasks: TaskResponse[] = [];
  @Input() loading = false;
  @Output() cancelTask = new EventEmitter<string>();
  @Output() deleteTask = new EventEmitter<string>();
}
