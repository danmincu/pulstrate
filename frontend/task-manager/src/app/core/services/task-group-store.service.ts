import { Injectable, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, Observable, map, tap, catchError, of } from 'rxjs';
import { TaskGroupApiService } from './task-group-api.service';
import { TaskGroup, CreateTaskGroupRequest, UpdateTaskGroupRequest } from '../models/task-group.model';

@Injectable({ providedIn: 'root' })
export class TaskGroupStoreService {
  private readonly groupApi = inject(TaskGroupApiService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly groupsMap$ = new BehaviorSubject<Map<string, TaskGroup>>(new Map());
  private readonly loadingSubject$ = new BehaviorSubject<boolean>(false);
  private readonly errorSubject$ = new BehaviorSubject<string | null>(null);

  readonly groups$: Observable<TaskGroup[]> = this.groupsMap$.pipe(
    map(groupMap => Array.from(groupMap.values())),
    map(groups => groups.sort((a, b) => a.name.localeCompare(b.name)))
  );

  readonly loading$ = this.loadingSubject$.asObservable();
  readonly error$ = this.errorSubject$.asObservable();

  loadGroups(): void {
    this.loadingSubject$.next(true);
    this.errorSubject$.next(null);

    this.groupApi.getGroups().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(groups => {
        const groupMap = new Map<string, TaskGroup>();
        groups.forEach(g => groupMap.set(g.id, g));
        this.groupsMap$.next(groupMap);
        this.loadingSubject$.next(false);
      }),
      catchError(err => {
        console.error('Failed to load groups:', err);
        this.errorSubject$.next('Failed to load groups');
        this.loadingSubject$.next(false);
        return of([]);
      })
    ).subscribe();
  }

  createGroup(request: CreateTaskGroupRequest): Observable<TaskGroup> {
    return this.groupApi.createGroup(request).pipe(
      tap(group => {
        this.updateGroup(group);
      }),
      catchError(err => {
        console.error('Failed to create group:', err);
        this.errorSubject$.next('Failed to create group');
        throw err;
      })
    );
  }

  updateGroupById(id: string, request: UpdateTaskGroupRequest): Observable<TaskGroup> {
    return this.groupApi.updateGroup(id, request).pipe(
      tap(group => {
        this.updateGroup(group);
      }),
      catchError(err => {
        console.error('Failed to update group:', err);
        this.errorSubject$.next('Failed to update group');
        throw err;
      })
    );
  }

  deleteGroup(id: string): Observable<void> {
    return this.groupApi.deleteGroup(id).pipe(
      tap(() => {
        this.removeGroup(id);
      }),
      catchError(err => {
        console.error('Failed to delete group:', err);
        this.errorSubject$.next('Failed to delete group');
        throw err;
      })
    );
  }

  getGroupById(id: string): Observable<TaskGroup | undefined> {
    return this.groupsMap$.pipe(
      map(groupMap => groupMap.get(id))
    );
  }

  private updateGroup(group: TaskGroup): void {
    const groups = new Map(this.groupsMap$.value);
    groups.set(group.id, group);
    this.groupsMap$.next(groups);
  }

  private removeGroup(groupId: string): void {
    const groups = new Map(this.groupsMap$.value);
    groups.delete(groupId);
    this.groupsMap$.next(groups);
  }
}
