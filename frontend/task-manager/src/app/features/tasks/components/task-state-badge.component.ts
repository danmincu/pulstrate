import { Component, Input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { TaskState } from '../../../core/models/task.model';

@Component({
  selector: 'app-task-state-badge',
  standalone: true,
  imports: [MatChipsModule],
  template: `
    <mat-chip-set>
      <mat-chip [class]="stateClass" [highlighted]="isHighlighted">
        {{ state }}
      </mat-chip>
    </mat-chip-set>
  `,
  styles: [`
    mat-chip-set {
      display: inline-block;
    }

    .state-queued {
      --mdc-chip-elevated-container-color: #e3f2fd;
      --mdc-chip-label-text-color: #1976d2;
    }

    .state-executing {
      --mdc-chip-elevated-container-color: #fff3e0;
      --mdc-chip-label-text-color: #f57c00;
    }

    .state-completed {
      --mdc-chip-elevated-container-color: #e8f5e9;
      --mdc-chip-label-text-color: #388e3c;
    }

    .state-cancelled {
      --mdc-chip-elevated-container-color: #fafafa;
      --mdc-chip-label-text-color: #757575;
    }

    .state-errored, .state-terminated {
      --mdc-chip-elevated-container-color: #ffebee;
      --mdc-chip-label-text-color: #d32f2f;
    }
  `]
})
export class TaskStateBadgeComponent {
  @Input() state: TaskState = 'Queued';

  get stateClass(): string {
    return `state-${this.state.toLowerCase()}`;
  }

  get isHighlighted(): boolean {
    return this.state === 'Executing';
  }
}
