import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TaskGroup, CreateTaskGroupRequest, UpdateTaskGroupRequest } from '../models/task-group.model';

@Injectable({ providedIn: 'root' })
export class TaskGroupApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/v1/dev/groups`;

  getGroups(): Observable<TaskGroup[]> {
    return this.http.get<TaskGroup[]>(this.apiUrl);
  }

  getGroup(id: string): Observable<TaskGroup> {
    return this.http.get<TaskGroup>(`${this.apiUrl}/${id}`);
  }

  createGroup(request: CreateTaskGroupRequest): Observable<TaskGroup> {
    return this.http.post<TaskGroup>(this.apiUrl, request);
  }

  updateGroup(id: string, request: UpdateTaskGroupRequest): Observable<TaskGroup> {
    return this.http.put<TaskGroup>(`${this.apiUrl}/${id}`, request);
  }

  deleteGroup(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
