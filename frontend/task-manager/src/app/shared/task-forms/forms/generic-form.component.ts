import { Component } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { BaseTaskFormComponent } from '../base-task-form.component';

function jsonValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value || control.value.trim() === '') {
    return null;
  }
  try {
    JSON.parse(control.value);
    return null;
  } catch {
    return { invalidJson: true };
  }
}

@Component({
  selector: 'app-generic-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule
  ],
  template: `
    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Task Type Name</mat-label>
      <input matInput [formControl]="taskTypeControl" placeholder="e.g., myCustomTask">
      <mat-hint>Enter the exact task type name your executor expects</mat-hint>
      @if (taskTypeControl.hasError('required')) {
        <mat-error>Task type is required</mat-error>
      }
      @if (taskTypeControl.hasError('pattern')) {
        <mat-error>Task type must be alphanumeric (letters, numbers, hyphens, underscores)</mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline" class="full-width payload-field">
      <mat-label>JSON Payload</mat-label>
      <textarea matInput
                [formControl]="payloadControl"
                rows="6"
                [placeholder]="jsonPlaceholder"></textarea>
      <mat-hint>Enter valid JSON or leave empty for empty object</mat-hint>
      @if (payloadControl.hasError('invalidJson')) {
        <mat-error>Invalid JSON format</mat-error>
      }
    </mat-form-field>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .full-width {
      width: 100%;
    }

    .payload-field textarea {
      font-family: 'Consolas', 'Monaco', monospace;
      font-size: 13px;
    }
  `]
})
export class GenericFormComponent extends BaseTaskFormComponent {
  readonly jsonPlaceholder = '{ "key": "value" }';

  taskTypeControl = new FormControl('', [
    Validators.required,
    Validators.pattern(/^[a-zA-Z][a-zA-Z0-9_-]*$/)
  ]);

  payloadControl = new FormControl('{}', [jsonValidator]);

  override form = new FormGroup({
    taskType: this.taskTypeControl,
    payload: this.payloadControl
  });

  override getCustomTaskType(): string {
    return this.taskTypeControl.value || '';
  }

  override getPayload(): object {
    const value = this.payloadControl.value?.trim();
    if (!value || value === '') {
      return {};
    }
    try {
      return JSON.parse(value);
    } catch {
      return {};
    }
  }
}
