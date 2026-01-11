import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateTaskRequest, CreateTaskHierarchyRequest, TaskResponse } from '../models/task.model';

@Injectable({ providedIn: 'root' })
export class TaskApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/v1/tasks`;

  getTasks(): Observable<TaskResponse[]> {
    return this.http.get<TaskResponse[]>(this.apiUrl);
  }

  getTask(id: string): Observable<TaskResponse> {
    return this.http.get<TaskResponse>(`${this.apiUrl}/${id}`);
  }

  createTask(request: CreateTaskRequest): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(this.apiUrl, request);
  }

  cancelTask(id: string): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(`${this.apiUrl}/${id}/cancel`, {});
  }

  deleteTask(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  // Hierarchical task endpoints

  createTaskHierarchy(request: CreateTaskHierarchyRequest): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(`${this.apiUrl}/hierarchy`, request);
  }

  getChildTasks(parentId: string): Observable<TaskResponse[]> {
    return this.http.get<TaskResponse[]>(`${this.apiUrl}/${parentId}/children`);
  }

  cancelTaskSubtree(id: string): Observable<TaskResponse> {
    return this.http.post<TaskResponse>(`${this.apiUrl}/${id}/cancel-subtree`, {});
  }

  deleteTaskSubtree(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}/subtree`);
  }
}
