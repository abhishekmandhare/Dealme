export interface AutomationTask {
  id: string
  name: string
  description: string
  schedule: string
  maxPointsPerDay: number
}

export interface AutomationProvider {
  id: string
  name: string
  description: string
  tasks: AutomationTask[]
  loginRequired: boolean
  loginInstructions: string
  serviceHealthy: boolean
  lastRun: AutomationRun | null
}

export interface AutomationRun {
  id: string
  provider: string
  task: string
  success: boolean
  itemsCompleted: number
  itemsTotal: number
  pointsBefore: number | null
  pointsAfter: number | null
  error: string | null
  startedAt: string
  completedAt: string
}

export interface AutomationStats {
  currentPoints: number | null
  today: { runs: number; pointsEstimate: number; success: number }
  week: { runs: number; pointsEstimate: number; success: number }
  month: { runs: number; pointsEstimate: number; success: number }
  totalRuns: number
}

export interface AutomationConfig {
  providerId: string
  accountLabel: string | null
  cronSchedule: string
  searchCount: number
  mobileSearchCount: number
  minDelayMs: number
  maxDelayMs: number
  enabled: boolean
}
