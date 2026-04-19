export interface AvailableCompetition {
  id: string
  title: string
  url: string
  description: string | null
  source: string
  createdAt: string
  expiresAt: string | null
}

export interface CompetitionEntry {
  id: string
  opportunityId: string
  title: string
  competitionUrl: string
  sourceUrl: string
  source: string
  status: 'Pending' | 'Entered' | 'Skipped' | 'Failed'
  reason: string | null
  emailUsed: string | null
  attemptedAt: string | null
  expiresAt: string | null
  createdAt: string
}

export interface CompetitionStats {
  available: number
  entered: number
  skipped: number
  failed: number
}
