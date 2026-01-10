import { Directive } from '@angular/core';
import { FormGroup } from '@angular/forms';

@Directive()
export abstract class BaseTaskFormComponent {
  form?: FormGroup;

  abstract getPayload(): object;

  isValid(): boolean {
    return this.form?.valid ?? true;
  }

  markAsTouched(): void {
    this.form?.markAllAsTouched();
  }

  /**
   * Override this method to provide a custom task type.
   * Used by generic form to allow user-specified task types.
   */
  getCustomTaskType(): string | null {
    return null;
  }

  /**
   * Override to return true if this form creates a hierarchical task.
   */
  isHierarchical(): boolean {
    return false;
  }

  /**
   * Override to return the hierarchy request structure.
   * Only called if isHierarchical() returns true.
   */
  getHierarchyRequest(): { parentTask: any; childTasks: any[] } | null {
    return null;
  }
}
