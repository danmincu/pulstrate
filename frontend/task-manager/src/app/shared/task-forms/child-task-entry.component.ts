import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  inject,
  ViewChild,
  AfterViewInit,
  ChangeDetectorRef
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { TaskFormRegistryService, TaskFormConfig } from './task-form-registry.service';
import { TaskFormHostComponent } from './task-form-host.component';
import { BaseTaskFormComponent } from './base-task-form.component';
import { CreateTaskHierarchyRequest, CreateTaskRequest } from '../../core/models/task.model';
import { TaskGroup } from '../../core/models/task-group.model';

export interface ChildTaskData {
  taskType: string;
  weight: number;
  groupId: string | null;
  formComponent: BaseTaskFormComponent | null;
}

@Component({
  selector: 'app-child-task-entry',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatExpansionModule,
    TaskFormHostComponent
  ],
  template: `
    <mat-card class="child-entry-card">
      <mat-card-header>
        <mat-card-title class="child-header">
          <span class="child-label">Child Task {{ index + 1 }}</span>
          <div class="header-actions">
            @if (selectedTaskType) {
              <span class="type-badge">{{ getTaskDisplayName() }}</span>
            }
            <button mat-icon-button color="warn" (click)="remove.emit()" [disabled]="!canRemove">
              <mat-icon>delete</mat-icon>
            </button>
          </div>
        </mat-card-title>
      </mat-card-header>

      <mat-card-content>
        <div class="child-config">
          <mat-form-field appearance="outline" class="type-field">
            <mat-label>Task Type</mat-label>
            <mat-select [formControl]="taskTypeControl">
              @for (type of taskTypes; track type.taskType) {
                <mat-option [value]="type.taskType">{{ type.displayName }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="group-field">
            <mat-label>Group</mat-label>
            <mat-select [formControl]="groupIdControl">
              <mat-option [value]="null">Default</mat-option>
              @for (group of groups; track group.id) {
                <mat-option [value]="group.id">
                  {{ group.name }} (max: {{ group.maxParallelism }})
                </mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="weight-field">
            <mat-label>Weight</mat-label>
            <input matInput type="number" step="0.1" [formControl]="weightControl">
            <mat-hint>Progress contribution</mat-hint>
          </mat-form-field>
        </div>

        @if (selectedTaskType) {
          <div class="task-form-wrapper">
            <app-task-form-host
              [taskType]="selectedTaskType"
              (formReady)="onFormReady($event)">
            </app-task-form-host>
          </div>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .child-entry-card {
      margin-bottom: 12px;
      border-left: 3px solid #ff9800;
    }

    .child-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      width: 100%;
    }

    .child-label {
      font-size: 14px;
      font-weight: 500;
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .type-badge {
      font-size: 11px;
      padding: 2px 8px;
      border-radius: 12px;
      background-color: #e3f2fd;
      color: #1565c0;
      font-weight: 500;
    }

    .child-config {
      display: flex;
      gap: 16px;
      margin-bottom: 16px;
    }

    .type-field {
      flex: 2;
    }

    .group-field {
      flex: 2;
    }

    .weight-field {
      flex: 1;
      max-width: 100px;
    }

    .task-form-wrapper {
      padding: 16px;
      background: #fafafa;
      border-radius: 8px;
      border: 1px solid #e0e0e0;
    }
  `]
})
export class ChildTaskEntryComponent implements OnInit {
  @Input() index = 0;
  @Input() canRemove = true;
  @Input() defaultTaskType = '';
  @Input() groups: TaskGroup[] = [];

  @Output() remove = new EventEmitter<void>();
  @Output() dataChanged = new EventEmitter<ChildTaskData>();

  private readonly registry = inject(TaskFormRegistryService);
  private readonly cdr = inject(ChangeDetectorRef);

  taskTypes: TaskFormConfig[] = [];

  taskTypeControl = new FormControl('', Validators.required);
  groupIdControl = new FormControl<string | null>(null);
  weightControl = new FormControl(1.0, [Validators.required, Validators.min(0.1)]);

  private formComponent: BaseTaskFormComponent | null = null;

  ngOnInit(): void {
    this.taskTypes = this.registry.getAll();

    // Set default task type
    if (this.defaultTaskType && this.taskTypes.some(t => t.taskType === this.defaultTaskType)) {
      this.taskTypeControl.setValue(this.defaultTaskType);
    } else if (this.taskTypes.length > 0) {
      // Default to countdown if available, otherwise first option
      const countdown = this.taskTypes.find(t => t.taskType === 'countdown');
      this.taskTypeControl.setValue(countdown?.taskType || this.taskTypes[0].taskType);
    }

    // Listen for type changes
    this.taskTypeControl.valueChanges.subscribe(() => {
      this.formComponent = null;
      this.emitDataChanged();
    });

    this.groupIdControl.valueChanges.subscribe(() => {
      this.emitDataChanged();
    });

    this.weightControl.valueChanges.subscribe(() => {
      this.emitDataChanged();
    });
  }

  get selectedTaskType(): string {
    return this.taskTypeControl.value || '';
  }

  getTaskDisplayName(): string {
    const config = this.taskTypes.find(t => t.taskType === this.selectedTaskType);
    return config?.displayName || this.selectedTaskType;
  }

  onFormReady(component: BaseTaskFormComponent): void {
    this.formComponent = component;
    this.emitDataChanged();
    this.cdr.detectChanges();
  }

  isValid(): boolean {
    if (!this.taskTypeControl.valid || !this.weightControl.valid) {
      return false;
    }
    return this.formComponent?.isValid() ?? false;
  }

  private emitDataChanged(): void {
    this.dataChanged.emit({
      taskType: this.selectedTaskType,
      weight: this.weightControl.value || 1.0,
      groupId: this.groupIdControl.value,
      formComponent: this.formComponent
    });
  }

  /**
   * Get the hierarchy request data for this child task.
   * If this is a hierarchical form, it will include nested children.
   */
  getHierarchyData(): CreateTaskHierarchyRequest | null {
    if (!this.formComponent || !this.isValid()) {
      return null;
    }

    const taskType = this.formComponent.getCustomTaskType() || this.selectedTaskType;
    const weight = this.weightControl.value || 1.0;
    const groupId = this.groupIdControl.value || undefined;

    // Check if this child is itself a hierarchical task
    if (this.formComponent.isHierarchical()) {
      const nestedHierarchy = this.formComponent.getHierarchyRequest();
      if (nestedHierarchy) {
        return {
          parentTask: {
            ...nestedHierarchy.parentTask,
            weight,
            groupId
          },
          childTasks: nestedHierarchy.childTasks
        };
      }
    }

    // Regular (non-hierarchical) child task
    return {
      parentTask: {
        type: taskType,
        priority: 5, // Will be overridden by parent
        payload: JSON.stringify(this.formComponent.getPayload()),
        weight,
        groupId
      },
      childTasks: []
    };
  }
}
