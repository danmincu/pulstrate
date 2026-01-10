import { Component, OnInit, QueryList, ViewChildren, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { AsyncPipe } from '@angular/common';
import { BaseTaskFormComponent } from '../base-task-form.component';
import { ChildTaskEntryComponent, ChildTaskData } from '../child-task-entry.component';
import { CreateTaskHierarchyRequest } from '../../../core/models/task.model';
import { TaskGroupStoreService } from '../../../core/services/task-group-store.service';

interface ChildEntry {
  id: number;
  defaultTaskType: string;
}

@Component({
  selector: 'app-simple-hierarchical-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    AsyncPipe,
    ChildTaskEntryComponent
  ],
  template: `
    <div class="form-container">
      <div class="parent-section">
        <h4>Parent Task Configuration</h4>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Parent Name</mat-label>
          <input matInput [formControl]="parentNameControl" placeholder="My Hierarchical Task">
          <mat-hint>Optional name for this parent task</mat-hint>
        </mat-form-field>

        <mat-checkbox [formControl]="parallelControl">
          Execute children in parallel
        </mat-checkbox>
        <p class="checkbox-hint">
          {{ parallelControl.value ? 'Children will execute simultaneously' : 'Children will execute one after another' }}
        </p>
      </div>

      <mat-divider></mat-divider>

      <div class="children-section">
        <div class="section-header">
          <h4>Child Tasks</h4>
          <span class="child-count">{{ childEntries.length }} child{{ childEntries.length !== 1 ? 'ren' : '' }}</span>
        </div>
        <p class="hint">Add child tasks of any type - including nested hierarchical tasks</p>

        @for (entry of childEntries; track entry.id; let i = $index) {
          <app-child-task-entry
            [index]="i"
            [canRemove]="childEntries.length > 1"
            [defaultTaskType]="entry.defaultTaskType"
            [groups]="(groups$ | async) || []"
            (remove)="removeChild(i)"
            (dataChanged)="onChildDataChanged(i, $event)">
          </app-child-task-entry>
        }

        <button mat-stroked-button color="primary" (click)="addChild()" class="add-btn">
          <mat-icon>add</mat-icon>
          Add Child Task
        </button>
      </div>
    </div>
  `,
  styles: [`
    .form-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .parent-section {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .full-width {
      width: 100%;
    }

    h4 {
      margin: 8px 0 4px 0;
      color: rgba(0, 0, 0, 0.87);
    }

    .checkbox-hint {
      font-size: 12px;
      color: rgba(0, 0, 0, 0.54);
      margin: 0 0 8px 24px;
    }

    .children-section {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .child-count {
      font-size: 12px;
      padding: 4px 12px;
      border-radius: 16px;
      background-color: #e8eaf6;
      color: #3f51b5;
      font-weight: 500;
    }

    .hint {
      font-size: 12px;
      color: rgba(0, 0, 0, 0.54);
      margin: 0;
    }

    .add-btn {
      align-self: flex-start;
    }
  `]
})
export class SimpleHierarchicalFormComponent extends BaseTaskFormComponent implements OnInit {
  @ViewChildren(ChildTaskEntryComponent) childComponents!: QueryList<ChildTaskEntryComponent>;

  private readonly groupStore = inject(TaskGroupStoreService);

  parentNameControl = new FormControl('My Hierarchical Task', { nonNullable: true });
  parallelControl = new FormControl(true, { nonNullable: true });

  groups$ = this.groupStore.groups$;

  childEntries: ChildEntry[] = [];
  private childDataMap = new Map<number, ChildTaskData>();
  private nextChildId = 0;

  ngOnInit(): void {
    // Load groups for child task selection
    this.groupStore.loadGroups();

    // Start with 2 children by default
    this.addChild('countdown');
    this.addChild('countdown');
  }

  addChild(defaultType = 'countdown'): void {
    this.childEntries.push({
      id: this.nextChildId++,
      defaultTaskType: defaultType
    });
  }

  removeChild(index: number): void {
    if (this.childEntries.length > 1) {
      const entry = this.childEntries[index];
      this.childDataMap.delete(entry.id);
      this.childEntries.splice(index, 1);
    }
  }

  onChildDataChanged(index: number, data: ChildTaskData): void {
    const entry = this.childEntries[index];
    if (entry) {
      this.childDataMap.set(entry.id, data);
    }
  }

  override isValid(): boolean {
    if (this.childEntries.length === 0) {
      return false;
    }

    // Check all child components are valid
    if (!this.childComponents) {
      return false;
    }

    return this.childComponents.toArray().every(child => child.isValid());
  }

  override getPayload(): object {
    return {
      parentName: this.parentNameControl.value,
      subtaskParallelism: this.parallelControl.value,
      childCount: this.childEntries.length
    };
  }

  override isHierarchical(): boolean {
    return true;
  }

  override getHierarchyRequest(): CreateTaskHierarchyRequest | null {
    if (!this.childComponents || this.childComponents.length === 0) {
      return null;
    }

    const childTasks: CreateTaskHierarchyRequest[] = [];

    // Collect hierarchy data from each child component
    this.childComponents.forEach(childComponent => {
      const childData = childComponent.getHierarchyData();
      if (childData) {
        childTasks.push(childData);
      }
    });

    if (childTasks.length === 0) {
      return null;
    }

    return {
      parentTask: {
        type: 'hierarchical-parent',
        priority: 5, // Will be overridden by dialog
        payload: JSON.stringify({
          parentName: this.parentNameControl.value
        }),
        subtaskParallelism: this.parallelControl.value
      },
      childTasks
    };
  }
}
