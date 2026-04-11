export interface AdapterStatus {
  adapterName: string
  isHealthy: boolean
  lastRunAt: string | null
  itemsLastRun: number
  lastError: string | null
}
