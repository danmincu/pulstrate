import { ApplicationConfig, APP_INITIALIZER, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';
import { TaskFormRegistryService } from './shared/task-forms/task-form-registry.service';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { CountdownFormComponent } from './shared/task-forms/forms/countdown-form.component';
import { GenericFormComponent } from './shared/task-forms/forms/generic-form.component';
import { RollDiceFormComponent } from './shared/task-forms/forms/rolldice-form.component';
import { SimpleHierarchicalFormComponent } from './shared/task-forms/forms/simple-hierarchical-form.component';

function initializeTaskForms(registry: TaskFormRegistryService): () => void {
  return () => {
    registry.register({
      taskType: 'countdown',
      displayName: 'Countdown Timer',
      component: CountdownFormComponent
    });
    registry.register({
      taskType: 'simple-hierarchical',
      displayName: 'Hierarchical Task',
      component: SimpleHierarchicalFormComponent
    });
    registry.register({
      taskType: 'generic',
      displayName: 'Generic (Custom Type)',
      component: GenericFormComponent
    });
    registry.register({
      taskType: 'rolldice',
      displayName: 'Roll Dice',
      component: RollDiceFormComponent
    });
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeTaskForms,
      deps: [TaskFormRegistryService],
      multi: true
    }
  ]
};
