import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewContainerRef,
  inject,
  OnChanges,
  SimpleChanges,
  ComponentRef
} from '@angular/core';
import { TaskFormRegistryService } from './task-form-registry.service';
import { BaseTaskFormComponent } from './base-task-form.component';

@Component({
  selector: 'app-task-form-host',
  standalone: true,
  template: ''
})
export class TaskFormHostComponent implements OnChanges {
  @Input() taskType!: string;
  @Output() formReady = new EventEmitter<BaseTaskFormComponent>();

  private readonly registry = inject(TaskFormRegistryService);
  private readonly viewContainerRef = inject(ViewContainerRef);

  private componentRef: ComponentRef<BaseTaskFormComponent> | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['taskType']) {
      this.loadForm();
    }
  }

  private loadForm(): void {
    this.viewContainerRef.clear();
    this.componentRef = null;

    if (!this.taskType) {
      return;
    }

    const config = this.registry.get(this.taskType);
    if (!config) {
      console.warn(`No form registered for task type: ${this.taskType}`);
      return;
    }

    this.componentRef = this.viewContainerRef.createComponent(config.component);
    this.formReady.emit(this.componentRef.instance);
  }
}
