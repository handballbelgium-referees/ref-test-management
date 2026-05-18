export interface AuditLogEntry {
  seqId: number;
  id: string;
  streamId: string;
  version: number;
  data?: string | null;
  type: string;
  timestamp: string;
  actorName: string;
  actorEmail: string;
  headers?: string | null;
  nodeId?: string | null;
}

export interface ParsedChange {
  property: string;
  value: unknown;
}
