export type OpportunityType = 'Deal' | 'Cashback' | 'Bonus' | 'Rebate' | 'PriceAlert'
export type OpportunityStatus = 'New' | 'Seen' | 'Saved' | 'ActedOn' | 'Dismissed' | 'Expired'
export type ConfidenceLevel = 'High' | 'Medium' | 'Low'

export interface Opportunity {
  id: string
  source: string
  sourceId: string
  type: OpportunityType
  status: OpportunityStatus
  title: string
  description: string | null
  value: number | null
  originalValue: number | null
  discountPercent: number | null
  url: string
  imageUrl: string | null
  expiresAt: string | null
  confidence: ConfidenceLevel
  tags: string[]
  metadata: Record<string, unknown>
  createdAt: string
  updatedAt: string
}
