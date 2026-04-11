export interface PriceWatch {
  id: string
  name: string
  searchQuery: string
  cheapestUrl: string | null
  cheapestRetailer: string | null
  targetPrice: number
  currentPrice: number | null
  isActive: boolean
  lastCheckedAt: string | null
  createdAt: string
  updatedAt: string
}
