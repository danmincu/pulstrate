export interface TaskGroup {
  id: string;
  name: string;
  maxParallelism: number;
  description: string | null;
  createdAt: string;
  updatedAt: string;
  activeTaskCount: number;
  queuedTaskCount: number;
}

export interface CreateTaskGroupRequest {
  id?: string;
  name: string;
  maxParallelism: number;
  description?: string;
}

export interface UpdateTaskGroupRequest {
  name?: string;
  maxParallelism?: number;
  description?: string;
}
