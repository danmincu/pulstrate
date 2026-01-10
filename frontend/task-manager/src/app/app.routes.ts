import { Routes } from '@angular/router';
import { TasksPageComponent } from './features/tasks/pages/tasks-page.component';

export const routes: Routes = [
  { path: '', redirectTo: 'tasks', pathMatch: 'full' },
  { path: 'tasks', component: TasksPageComponent },
  { path: '**', redirectTo: 'tasks' }
];
