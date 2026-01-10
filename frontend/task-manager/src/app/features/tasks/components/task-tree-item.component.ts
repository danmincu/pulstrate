import { Component, Input, Output, EventEmitter } from '@angular/core';
import { DatePipe, TitleCasePipe, DecimalPipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TaskTreeNode, isTerminalState } from '../../../core/models/task.model';
import { TaskStateBadgeComponent } from './task-state-badge.component';
import { TaskProgressComponent } from './task-progress.component';

@Component({
  selector: 'app-task-tree-item',
  standalone: true,
  imports: [
    DatePipe,
    TitleCasePipe,
    DecimalPipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    TaskStateBadgeComponent,
    TaskProgressComponent
  ],
  template: `
    <div class="tree-item" [style.--level]="node.level">
      <mat-card class="task-card" [class.terminal]="isTerminal" [class.has-children]="hasChildren"
                [style.backgroundColor]="levelBackground">
        <mat-card-header>
          @if (hasChildren) {
            <button mat-icon-button class="expand-btn" (click)="toggleExpand.emit(node.task.id)">
              <mat-icon>{{ node.expanded ? 'expand_more' : 'chevron_right' }}</mat-icon>
            </button>
          } @else {
            <div class="expand-placeholder"></div>
          }

          <div class="header-content">
            <mat-card-title>{{ node.task.type | titlecase }}</mat-card-title>
            <mat-card-subtitle class="task-id">{{ node.task.id }}</mat-card-subtitle>
          </div>

          <div class="badge-container">
            <app-task-state-badge [state]="node.task.state"></app-task-state-badge>
            <span class="group-badge">{{ node.task.groupName }}</span>
            @if (hasChildren) {
              <span class="child-badge" matTooltip="Number of child tasks">
                <mat-icon>account_tree</mat-icon>
                {{ node.task.childCount || node.children.length }}
              </span>
              <span class="execution-mode-badge" [class.parallel]="node.task.subtaskParallelism"
                    [matTooltip]="node.task.subtaskParallelism ? 'Children execute in parallel' : 'Children execute sequentially'">
                <mat-icon>{{ node.task.subtaskParallelism ? 'call_split' : 'format_list_numbered' }}</mat-icon>
                {{ node.task.subtaskParallelism ? 'Parallel' : 'Sequential' }}
              </span>
            }
            @if (node.task.parentTaskId) {
              <span class="weight-badge" matTooltip="Weight for progress contribution">
                {{ node.task.weight | number:'1.1-1' }}x
              </span>
            }
          </div>
        </mat-card-header>

        <mat-card-content>
          <div class="content-layout" [class.has-history]="hasChildren && node.level === 0">
            <!-- Left side: Progress and timestamps -->
            <div class="main-content">
              @if (hasChildren) {
                <div class="aggregated-progress">
                  <span class="progress-label">Aggregated Progress:</span>
                  <app-task-progress
                    [progress]="node.aggregatedProgress"
                    [state]="node.task.state">
                  </app-task-progress>
                </div>
              } @else {
                <app-task-progress
                  [progress]="node.task.progress"
                  [details]="node.task.progressDetails"
                  [state]="node.task.state">
                </app-task-progress>
              }

              @if (node.task.stateDetails) {
                <p class="state-details">{{ node.task.stateDetails }}</p>
              }

              <div class="timestamps">
                <span>Created: {{ node.task.createdAt | date:'short' }}</span>
                @if (node.task.startedAt) {
                  <span>Started: {{ node.task.startedAt | date:'short' }}</span>
                }
                @if (node.task.completedAt) {
                  <span>Completed: {{ node.task.completedAt | date:'short' }}</span>
                }
              </div>
            </div>

            <!-- Right side: History boxes (only for root hierarchical tasks) -->
            @if (hasChildren && node.level === 0) {
              <div class="history-sidebar">
                <!-- Progress History Box -->
                <div class="history-box progress-history">
                  <div class="history-label">
                    <mat-icon>trending_up</mat-icon>
                    Child Progress
                  </div>
                  <div class="history-content">
                    @if (node.progressHistory.length === 0) {
                      <div class="history-empty">No progress updates yet</div>
                    } @else {
                      @for (entry of getRecentProgressHistory(); track $index) {
                        <div class="history-entry">
                          <span class="entry-time">{{ entry.timestamp | date:'HH:mm:ss' }}</span>
                          <span class="entry-type">{{ entry.taskType }}</span>
                          <span class="entry-text">{{ entry.displayText }}</span>
                        </div>
                      }
                    }
                  </div>
                </div>

                <!-- State Change History Box -->
                <div class="history-box state-history">
                  <div class="history-label">
                    <mat-icon>history</mat-icon>
                    Status Changes
                  </div>
                  <div class="history-content">
                    @if (node.stateChangeHistory.length === 0) {
                      <div class="history-empty">No status changes yet</div>
                    } @else {
                      @for (entry of getRecentStateHistory(); track $index) {
                        <div class="history-entry">
                          <span class="entry-time">{{ entry.timestamp | date:'HH:mm:ss' }}</span>
                          <span class="entry-id">{{ entry.taskIdShort }}...</span>
                          <span class="entry-type">{{ entry.taskType }}</span>
                          <span class="entry-state" [class]="'state-' + entry.newState.toLowerCase()">
                            {{ entry.newState }}
                          </span>
                        </div>
                      }
                    }
                  </div>
                </div>
              </div>
            }
          </div>
        </mat-card-content>

        <mat-card-actions>
          @if (!isTerminal) {
            <button mat-button color="warn" (click)="hasChildren ? cancelSubtree.emit() : cancel.emit()">
              <mat-icon>cancel</mat-icon>
              <span>Cancel</span>
            </button>
          }
          @if (isTerminal) {
            <button mat-button color="warn" (click)="hasChildren ? deleteSubtree.emit() : delete.emit()">
              <mat-icon>delete</mat-icon>
              <span>Delete</span>
            </button>
          }
        </mat-card-actions>
      </mat-card>
    </div>

    @if (node.expanded && hasChildren) {
      @for (child of node.children; track child.task.id) {
        <app-task-tree-item
          [node]="child"
          (toggleExpand)="toggleExpand.emit($event)"
          (cancel)="childCancel.emit(child.task.id)"
          (cancelSubtree)="childCancelSubtree.emit(child.task.id)"
          (delete)="childDelete.emit(child.task.id)"
          (deleteSubtree)="childDeleteSubtree.emit(child.task.id)">
        </app-task-tree-item>
      }
    }
  `,
  styles: [`
    .tree-item {
      position: relative;
      margin-left: calc(var(--level, 0) * 24px);
    }

    .task-card {
      margin-bottom: 8px;
      transition: background-color 0.2s ease;
    }

    .task-card.terminal {
      opacity: 0.8;
    }

    .task-card.has-children {
      border-left: 3px solid #3f51b5;
    }

    mat-card-header {
      position: relative;
      align-items: flex-start;
      flex-wrap: wrap;
    }

    .header-content {
      flex: 1;
      min-width: 150px;
    }

    .expand-btn {
      margin-right: 8px;
      margin-left: -8px;
      flex-shrink: 0;
    }

    .expand-placeholder {
      width: 40px;
      margin-right: 8px;
      margin-left: -8px;
      flex-shrink: 0;
    }

    .task-id {
      font-family: monospace;
      font-size: 12px;
      word-break: break-all;
    }

    .badge-container {
      display: flex;
      gap: 8px;
      align-items: center;
      flex-wrap: wrap;
      margin-left: auto;
      padding-left: 16px;
    }

    .group-badge {
      font-size: 11px;
      padding: 2px 8px;
      border-radius: 12px;
      background-color: #e3f2fd;
      color: #1565c0;
      font-weight: 500;
      white-space: nowrap;
    }

    .child-badge {
      display: flex;
      align-items: center;
      gap: 2px;
      font-size: 11px;
      padding: 2px 8px;
      border-radius: 12px;
      background-color: #f3e5f5;
      color: #7b1fa2;
      font-weight: 500;
      white-space: nowrap;
    }

    .child-badge mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    .weight-badge {
      font-size: 11px;
      padding: 2px 8px;
      border-radius: 12px;
      background-color: #fff3e0;
      color: #e65100;
      font-weight: 500;
      white-space: nowrap;
    }

    .execution-mode-badge {
      display: flex;
      align-items: center;
      gap: 2px;
      font-size: 11px;
      padding: 2px 8px;
      border-radius: 12px;
      background-color: #fce4ec;
      color: #c2185b;
      font-weight: 500;
      white-space: nowrap;
    }

    .execution-mode-badge.parallel {
      background-color: #e8f5e9;
      color: #2e7d32;
    }

    .execution-mode-badge mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    /* Content Layout */
    .content-layout {
      display: block;
    }

    .content-layout.has-history {
      display: grid;
      grid-template-columns: 1fr 300px;
      gap: 16px;
    }

    .main-content {
      min-width: 0;
    }

    .aggregated-progress {
      margin-bottom: 8px;
    }

    .progress-label {
      font-size: 12px;
      color: rgba(0, 0, 0, 0.54);
      margin-bottom: 4px;
      display: block;
    }

    .state-details {
      color: rgba(0, 0, 0, 0.6);
      font-style: italic;
      margin: 8px 0;
      word-break: break-word;
    }

    .timestamps {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      font-size: 12px;
      color: rgba(0, 0, 0, 0.54);
      margin-top: 8px;
    }

    /* History Sidebar */
    .history-sidebar {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .history-box {
      border: 1px solid rgba(0, 0, 0, 0.12);
      border-radius: 4px;
      background: rgba(255, 255, 255, 0.7);
      overflow: hidden;
    }

    .history-label {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 10px;
      font-weight: 500;
      color: rgba(0, 0, 0, 0.54);
      padding: 4px 8px;
      border-bottom: 1px solid rgba(0, 0, 0, 0.08);
      background: rgba(0, 0, 0, 0.02);
    }

    .history-label mat-icon {
      font-size: 12px;
      width: 12px;
      height: 12px;
    }

    .history-content {
      max-height: 54px;
      overflow-y: auto;
      padding: 4px 8px;
      font-family: 'Roboto Mono', 'Consolas', monospace;
      font-size: 10px;
      line-height: 18px;
    }

    .history-empty {
      color: rgba(0, 0, 0, 0.3);
      font-style: italic;
      text-align: center;
      padding: 8px 0;
    }

    .history-entry {
      display: flex;
      gap: 6px;
      white-space: nowrap;
      overflow: hidden;
    }

    .entry-time {
      color: rgba(0, 0, 0, 0.4);
      flex-shrink: 0;
    }

    .entry-text {
      color: rgba(0, 0, 0, 0.7);
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .entry-id {
      color: rgba(0, 0, 0, 0.5);
      flex-shrink: 0;
    }

    .entry-type {
      color: #1565c0;
      flex-shrink: 0;
    }

    .entry-state {
      font-weight: 500;
      flex-shrink: 0;
    }

    .state-completed { color: #2e7d32; }
    .state-executing { color: #1565c0; }
    .state-queued { color: #757575; }
    .state-cancelled { color: #f57c00; }
    .state-errored { color: #c62828; }
    .state-terminated { color: #c62828; }

    mat-card-actions {
      padding: 8px 16px;
    }

    /* Responsive styles */
    @media (max-width: 768px) {
      .content-layout.has-history {
        grid-template-columns: 1fr;
      }

      .history-sidebar {
        flex-direction: row;
      }

      .history-box {
        flex: 1;
        min-width: 0;
      }
    }

    @media (max-width: 600px) {
      .tree-item {
        margin-left: calc(var(--level, 0) * 12px);
      }

      .badge-container {
        position: relative;
        width: 100%;
        padding-left: 48px;
        margin-top: 8px;
        margin-left: 0;
      }

      .group-badge, .child-badge, .weight-badge, .execution-mode-badge {
        font-size: 10px;
        padding: 1px 6px;
      }

      .timestamps {
        font-size: 10px;
        gap: 8px;
      }

      .history-sidebar {
        flex-direction: column;
      }

      .history-content {
        font-size: 9px;
        line-height: 16px;
        max-height: 48px;
      }
    }

    @media (max-width: 400px) {
      .tree-item {
        margin-left: calc(var(--level, 0) * 8px);
      }

      .task-id {
        font-size: 10px;
      }

      .history-content {
        font-size: 8px;
        line-height: 14px;
      }

      .entry-id, .entry-type {
        display: none;
      }
    }
  `]
})
export class TaskTreeItemComponent {
  @Input() node!: TaskTreeNode;

  @Output() toggleExpand = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();
  @Output() cancelSubtree = new EventEmitter<void>();
  @Output() delete = new EventEmitter<void>();
  @Output() deleteSubtree = new EventEmitter<void>();

  // Pass-through events for child items
  @Output() childCancel = new EventEmitter<string>();
  @Output() childCancelSubtree = new EventEmitter<string>();
  @Output() childDelete = new EventEmitter<string>();
  @Output() childDeleteSubtree = new EventEmitter<string>();

  // Background color shading based on nesting level
  private readonly levelShades = [
    '#ffffff',  // Level 0 - white
    '#fafafa',  // Level 1
    '#f5f5f5',  // Level 2
    '#eeeeee',  // Level 3
    '#e8e8e8',  // Level 4
    '#e0e0e0',  // Level 5+
  ];

  get levelBackground(): string {
    return this.levelShades[Math.min(this.node.level, this.levelShades.length - 1)];
  }

  get isTerminal(): boolean {
    return isTerminalState(this.node.task.state);
  }

  get hasChildren(): boolean {
    return this.node.children.length > 0 || this.node.task.childCount > 0;
  }

  // Get most recent progress history entries (reversed so newest is first)
  getRecentProgressHistory() {
    return this.node.progressHistory.slice(-20).reverse();
  }

  // Get most recent state change history entries (reversed so newest is first)
  getRecentStateHistory() {
    return this.node.stateChangeHistory.slice(-20).reverse();
  }
}
