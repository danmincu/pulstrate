import { Component } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { BaseTaskFormComponent } from '../base-task-form.component';

@Component({
  selector: 'app-countdown-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule
  ],
  template: `
    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Duration (seconds)</mat-label>
      <input matInput type="number" [formControl]="durationControl" min="1" max="3600">
      <mat-hint>Enter countdown duration (1-3600 seconds)</mat-hint>
      @if (durationControl.hasError('required')) {
        <mat-error>Duration is required</mat-error>
      }
      @if (durationControl.hasError('min')) {
        <mat-error>Minimum is 1 second</mat-error>
      }
      @if (durationControl.hasError('max')) {
        <mat-error>Maximum is 3600 seconds</mat-error>
      }
    </mat-form-field>
  `,
  styles: [`
    .full-width {
      width: 100%;
    }
  `]
})
export class CountdownFormComponent extends BaseTaskFormComponent {
  durationControl = new FormControl(60, [
    Validators.required,
    Validators.min(1),
    Validators.max(3600)
  ]);

  override form = new FormGroup({
    durationInSeconds: this.durationControl
  });

  override getPayload(): object {
    return {
      durationInSeconds: this.durationControl.value
    };
  }
}
