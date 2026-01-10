import { Component, OnInit, inject } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { AsyncPipe } from '@angular/common';
import { TaskFormHostComponent } from '../../../shared/task-forms/task-form-host.component';
import { TaskFormRegistryService, TaskFormConfig } from '../../../shared/task-forms/task-form-registry.service';
import { BaseTaskFormComponent } from '../../../shared/task-forms/base-task-form.component';
import { TaskStoreService } from '../../../core/services/task-store.service';
import { TaskGroupStoreService } from '../../../core/services/task-group-store.service';
import { CreateTaskRequest, CreateTaskHierarchyRequest } from '../../../core/models/task.model';

@Component({
  selector: 'app-create-task-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    AsyncPipe,
    TaskFormHostComponent
  ],
  template: `
    <h2 mat-dialog-title>Create New Task</h2>

    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Task Type</mat-label>
          <mat-select formControlName="taskType">
            @for (type of taskTypes; track type.taskType) {
              <mat-option [value]="type.taskType">{{ type.displayName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Priority</mat-label>
          <input matInput type="number" formControlName="priority" min="0" max="100">
          <mat-hint>0 (lowest) to 100 (highest)</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Processing Group</mat-label>
          <mat-select formControlName="groupId">
            @for (group of groups$ | async; track group.id) {
              <mat-option [value]="group.id">
                {{ group.name }} (max: {{ group.maxParallelism }})
              </mat-option>
            }
          </mat-select>
          <mat-hint>Select which processing group should handle this task</mat-hint>
        </mat-form-field>

        @if (selectedTaskType) {
          <div class="task-form-container">
            <app-task-form-host
              [taskType]="selectedTaskType"
              (formReady)="onTaskFormReady($event)">
            </app-task-form-host>
          </div>
        }
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="primary"
              [disabled]="!canSubmit() || submitting"
              (click)="submit()">
        {{ submitting ? 'Creating...' : 'Create Task' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      max-height: 80vh;
    }

    :host ::ng-deep .mat-mdc-dialog-content {
      overflow-y: auto;
      overflow-x: hidden;
      padding-top: 8px;
      max-height: calc(80vh - 140px);
      flex: 1;
    }

    :host ::ng-deep .mat-mdc-dialog-actions {
      position: sticky;
      bottom: 0;
      background: white;
      padding: 16px 24px;
      margin: 0;
      border-top: 1px solid rgba(0, 0, 0, 0.12);
      z-index: 1;
    }

    .dialog-form {
      display: flex;
      flex-direction: column;
      min-width: 450px;
      gap: 8px;
      padding-top: 8px;
    }

    .full-width {
      width: 100%;
    }

    .task-form-container {
      margin-top: 16px;
    }
  `]
})
export class CreateTaskDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<CreateTaskDialogComponent>);
  private readonly registry = inject(TaskFormRegistryService);
  private readonly taskStore = inject(TaskStoreService);
  private readonly groupStore = inject(TaskGroupStoreService);

  form = new FormGroup({
    taskType: new FormControl('', Validators.required),
    priority: new FormControl(5, [Validators.required, Validators.min(0), Validators.max(100)]),
    groupId: new FormControl<string | null>(null)
  });

  taskTypes: TaskFormConfig[] = [];
  taskFormComponent: BaseTaskFormComponent | null = null;
  submitting = false;
  groups$ = this.groupStore.groups$;

  ngOnInit(): void {
    this.taskTypes = this.registry.getAll();
    if (this.taskTypes.length > 0) {
      this.form.get('taskType')?.setValue(this.taskTypes[0].taskType);
    }
    this.groupStore.loadGroups();
  }

  get selectedTaskType(): string {
    return this.form.get('taskType')?.value || '';
  }

  onTaskFormReady(formComponent: BaseTaskFormComponent): void {
    this.taskFormComponent = formComponent;
  }

  canSubmit(): boolean {
    return this.form.valid && (this.taskFormComponent?.isValid() ?? false);
  }

  submit(): void {
    if (!this.canSubmit() || !this.taskFormComponent || this.submitting) {
      return;
    }

    this.submitting = true;

    // Check if this is a hierarchical task form
    if (this.taskFormComponent.isHierarchical()) {
      this.submitHierarchical();
    } else {
      this.submitSingle();
    }
  }

  private submitSingle(): void {
    // Use custom task type from form if available (generic form), otherwise use dropdown value
    const customType = this.taskFormComponent!.getCustomTaskType();
    const taskType = customType || this.form.get('taskType')?.value || '';
    const groupId = this.form.get('groupId')?.value;

    const request: CreateTaskRequest = {
      type: taskType,
      priority: this.form.get('priority')?.value || 5,
      payload: JSON.stringify(this.taskFormComponent!.getPayload()),
      groupId: groupId || undefined
    };

    this.taskStore.createTask(request).subscribe({
      next: () => {
        this.dialogRef.close(true);
      },
      error: (err) => {
        console.error('Failed to create task', err);
        this.submitting = false;
      }
    });
  }

  private submitHierarchical(): void {
    const hierarchyData = this.taskFormComponent!.getHierarchyRequest();
    if (!hierarchyData) {
      this.submitting = false;
      return;
    }

    const groupId = this.form.get('groupId')?.value;
    const priority = this.form.get('priority')?.value || 5;

    // Apply priority and groupId to parent task only
    // Children keep their own groupId (selected in child-task-entry)
    const request: CreateTaskHierarchyRequest = {
      parentTask: {
        ...hierarchyData.parentTask,
        priority,
        groupId: groupId || undefined
      },
      childTasks: hierarchyData.childTasks.map(child => this.applyGroupToHierarchy(child, groupId, false))
    };

    this.taskStore.createTaskHierarchy(request).subscribe({
      next: () => {
        this.dialogRef.close(true);
      },
      error: (err) => {
        console.error('Failed to create hierarchical task', err);
        this.submitting = false;
      }
    });
  }

  private applyGroupToHierarchy(node: any, groupId: string | null | undefined, isRoot: boolean = true): any {
    return {
      parentTask: {
        ...node.parentTask,
        // Only apply groupId to root task. Children keep their own groupId (or default)
        // so they can run in parallel when SubtaskParallelism=true
        groupId: isRoot ? (groupId || undefined) : node.parentTask.groupId
      },
      childTasks: node.childTasks.map((child: any) => this.applyGroupToHierarchy(child, groupId, false))
    };
  }
}
