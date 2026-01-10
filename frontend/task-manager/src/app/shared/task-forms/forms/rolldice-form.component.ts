import { Component } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { BaseTaskFormComponent } from '../base-task-form.component';

@Component({
  selector: 'app-rolldice-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule
  ],
  template: `
    <div class="dice-container">
      <div class="dice-info">
        <p class="description">
          Roll two dice every second until you hit your desired combination.
          You have 100 rolls to get lucky!
        </p>
      </div>

      <div class="dice-selectors">
        <mat-form-field appearance="outline" class="dice-field">
          <mat-label>First Die</mat-label>
          <mat-select [formControl]="dice1Control">
            @for (value of diceValues; track value) {
              <mat-option [value]="value">
                <span class="dice-option">{{ getDiceEmoji(value) }} {{ value }}</span>
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        <span class="dice-separator">and</span>

        <mat-form-field appearance="outline" class="dice-field">
          <mat-label>Second Die</mat-label>
          <mat-select [formControl]="dice2Control">
            @for (value of diceValues; track value) {
              <mat-option [value]="value">
                <span class="dice-option">{{ getDiceEmoji(value) }} {{ value }}</span>
              </mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      <div class="target-display">
        <span class="target-label">Target:</span>
        <span class="target-dice">
          {{ getDiceEmoji(dice1Control.value) }}
          {{ getDiceEmoji(dice2Control.value) }}
        </span>
        <span class="target-text">({{ dice1Control.value }}-{{ dice2Control.value }})</span>
      </div>

      <div class="odds-info">
        <p>
          Odds of rolling {{ dice1Control.value }}-{{ dice2Control.value }}:
          <strong>{{ calculateOdds() }}</strong>
          ({{ getOddsPercentage() }}%)
        </p>
      </div>
    </div>
  `,
  styles: [`
    .dice-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .description {
      color: rgba(0, 0, 0, 0.6);
      margin: 0;
      font-size: 14px;
    }

    .dice-selectors {
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .dice-field {
      flex: 1;
    }

    .dice-separator {
      color: rgba(0, 0, 0, 0.6);
      font-size: 14px;
    }

    .dice-option {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .target-display {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 16px;
      background: #f5f5f5;
      border-radius: 8px;
    }

    .target-label {
      color: rgba(0, 0, 0, 0.6);
      font-size: 14px;
    }

    .target-dice {
      font-size: 32px;
    }

    .target-text {
      color: rgba(0, 0, 0, 0.6);
      font-size: 14px;
    }

    .odds-info {
      padding: 12px;
      background: #e3f2fd;
      border-radius: 8px;
    }

    .odds-info p {
      margin: 0;
      font-size: 13px;
      color: #1565c0;
    }
  `]
})
export class RollDiceFormComponent extends BaseTaskFormComponent {
  diceValues = [1, 2, 3, 4, 5, 6];

  dice1Control = new FormControl(6, [Validators.required, Validators.min(1), Validators.max(6)]);
  dice2Control = new FormControl(6, [Validators.required, Validators.min(1), Validators.max(6)]);

  override form = new FormGroup({
    desiredDice1: this.dice1Control,
    desiredDice2: this.dice2Control
  });

  getDiceEmoji(value: number | null): string {
    const diceEmojis: Record<number, string> = {
      1: '\u2680', // ⚀
      2: '\u2681', // ⚁
      3: '\u2682', // ⚂
      4: '\u2683', // ⚃
      5: '\u2684', // ⚄
      6: '\u2685'  // ⚅
    };
    return diceEmojis[value ?? 1] || '\u2680';
  }

  calculateOdds(): string {
    const d1 = this.dice1Control.value ?? 1;
    const d2 = this.dice2Control.value ?? 1;

    // If both dice are the same (e.g., 6-6), there's only 1 way to roll it
    // If different (e.g., 3-5), there are 2 ways (3-5 or 5-3)
    const ways = d1 === d2 ? 1 : 2;
    const total = 36; // 6 * 6 possible outcomes

    return `${ways} in ${total}`;
  }

  getOddsPercentage(): string {
    const d1 = this.dice1Control.value ?? 1;
    const d2 = this.dice2Control.value ?? 1;
    const ways = d1 === d2 ? 1 : 2;
    const percentage = (ways / 36) * 100;
    return percentage.toFixed(2);
  }

  override getPayload(): object {
    return {
      desiredDice1: this.dice1Control.value,
      desiredDice2: this.dice2Control.value
    };
  }
}
