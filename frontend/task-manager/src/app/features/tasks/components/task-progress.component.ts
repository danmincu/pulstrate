import { Component, Input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TaskState } from '../../../core/models/task.model';

@Component({
  selector: 'app-task-progress',
  standalone: true,
  imports: [DecimalPipe, MatProgressBarModule],
  template: `
    <div class="progress-container">
      <mat-progress-bar
        [mode]="mode"
        [value]="progress"
        [color]="color">
      </mat-progress-bar>
      <div class="progress-info">
        <span class="percentage">{{ progress | number:'1.0-0' }}%</span>
        @if (details) {
          <span class="details">{{ details }}</span>
        }
      </div>
    </div>
  `,
  styles: [`
    .progress-container {
      margin: 16px 0;
    }

    .progress-info {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 8px;
      font-size: 14px;
    }

    .percentage {
      font-weight: 500;
    }

    .details {
      color: rgba(0, 0, 0, 0.6);
    }
  `]
})
export class TaskProgressComponent {
  @Input() progress = 0;
  @Input() details: string | null = null;
  @Input() state: TaskState = 'Queued';

  get mode(): 'determinate' | 'indeterminate' {
    return this.state === 'Queued' ? 'indeterminate' : 'determinate';
  }

  get color(): 'primary' | 'accent' | 'warn' {
    if (this.state === 'Errored' || this.state === 'Terminated') return 'warn';
    if (this.state === 'Completed') return 'accent';
    return 'primary';
  }
}
