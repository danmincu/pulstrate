import { Injectable, Type } from '@angular/core';
import { BaseTaskFormComponent } from './base-task-form.component';

export interface TaskFormConfig {
  taskType: string;
  displayName: string;
  component: Type<BaseTaskFormComponent>;
}

@Injectable({ providedIn: 'root' })
export class TaskFormRegistryService {
  private readonly registry = new Map<string, TaskFormConfig>();

  register(config: TaskFormConfig): void {
    this.registry.set(config.taskType, config);
  }

  get(taskType: string): TaskFormConfig | undefined {
    return this.registry.get(taskType);
  }

  getAll(): TaskFormConfig[] {
    return Array.from(this.registry.values());
  }

  getTaskTypes(): string[] {
    return Array.from(this.registry.keys());
  }
}
