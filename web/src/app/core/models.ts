// Shapes returned by the Yard Ops API (camelCase JSON).

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }

export interface Trailer {
  trailerNumber: string; equipment: string; palletCapacity: number; status: string; spot: string | null; spotKind: string | null;
  currentLoadNumber: string | null; carrier: string | null; holdReason: string | null; updatedAt: string; actions: string[];
}

export interface Spot { code: string; kind: 'Parking' | 'Door'; trailer: Trailer | null; }

export interface GateEvent { id: string; trailerNumber: string; direction: string; occurredAt: string; note: string | null; }
export interface Move { id: string; trailerNumber: string; fromSpot: string | null; toSpot: string; occurredAt: string; }
export interface Inspection {
  id: string; trailerNumber: string; passed: boolean; occurredAt: string; notes: string | null;
  tires: boolean | null; lights: boolean | null; doorsAndSeal: boolean | null; floor: boolean | null; reeferUnit: boolean | null;
}
export interface YardEvent { eventId: string; eventType: string; trailerNumber: string; occurredAt: string; details: string | null; schemaVersion: number; }
export interface OutboxMessage {
  id: string; event: YardEvent; createdAt: string; attempts: number; nextAttemptAt: string; sentAt: string | null; lastError: string | null;
}
export interface TrailerDetail { trailer: Trailer; gate: GateEvent[]; moves: Move[]; inspections: Inspection[]; outbox: OutboxMessage[]; }
export interface LtlCandidate { orderId: string; customer: string; origin: string; destination: string; pallets: number; weight: number; equipment: string; reason: string; }

export interface Dashboard {
  byStatus: { status: string; count: number }[]; onYard: number; parkingFree: number; doorsInUse: number; doors: number;
  expected: number; gateInsToday: number; gateOutsToday: number; outboxPending: number; outboxFailing: number;
}

export interface Meta {
  equipmentTypes: string[]; trailerStatuses: string[];
  storage: { mode: string; persistent: boolean; ready: boolean };
  demoReset: { scheduled: boolean; schedule: string };
  ltl: { baseUrl: string };
  integration: { provider: string; mode: string; configured: boolean };
}

export interface ExternalTrailer { trailerNumber: string; status: string; equipmentType: string | null; equipmentSize: string | null; fleetName: string | null; source: string; }
export interface ExternalTrailerResult { provider: string; live: boolean; degraded: boolean; degradedReason?: string; trailers: ExternalTrailer[]; }
