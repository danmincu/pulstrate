import { Component, Input, Output, EventEmitter } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TaskTreeNode } from '../../../core/models/task.model';
import { TaskTreeItemComponent } from './task-tree-item.component';

@Component({
  selector: 'app-task-tree',
  standalone: true,
  imports: [
    MatCardModule,
    MatProgressSpinnerModule,
    MatButtonModule,
    MatIconModule,
    TaskTreeItemComponent
  ],
  template: `
    <div class="task-tree">
      @if (loading) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (nodes.length === 0) {
        <mat-card class="empty-state">
          <mat-card-content>
            <p>No tasks yet. Create one to get started!</p>
          </mat-card-content>
        </mat-card>
      } @else {
        <div class="tree-controls">
          <button mat-button (click)="expandAll.emit()">
            <mat-icon>unfold_more</mat-icon>
            <span>Expand All</span>
          </button>
          <button mat-button (click)="collapseAll.emit()">
            <mat-icon>unfold_less</mat-icon>
            <span>Collapse All</span>
          </button>
        </div>

        @for (node of nodes; track node.task.id) {
          <app-task-tree-item
            [node]="node"
            (toggleExpand)="toggleExpand.emit($event)"
            (cancel)="cancelTask.emit(node.task.id)"
            (cancelSubtree)="cancelSubtree.emit(node.task.id)"
            (delete)="deleteTask.emit(node.task.id)"
            (deleteSubtree)="deleteSubtree.emit(node.task.id)"
            (childCancel)="cancelTask.emit($event)"
            (childCancelSubtree)="cancelSubtree.emit($event)"
            (childDelete)="deleteTask.emit($event)"
            (childDeleteSubtree)="deleteSubtree.emit($event)">
          </app-task-tree-item>
        }
      }
    </div>
  `,
  styles: [`
    .task-tree {
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

    .tree-controls {
      display: flex;
      gap: 8px;
      margin-bottom: 16px;
    }
  `]
})
export class TaskTreeComponent {
  @Input() nodes: TaskTreeNode[] = [];
  @Input() loading = false;

  @Output() toggleExpand = new EventEmitter<string>();
  @Output() expandAll = new EventEmitter<void>();
  @Output() collapseAll = new EventEmitter<void>();
  @Output() cancelTask = new EventEmitter<string>();
  @Output() cancelSubtree = new EventEmitter<string>();
  @Output() deleteTask = new EventEmitter<string>();
  @Output() deleteSubtree = new EventEmitter<string>();
}
