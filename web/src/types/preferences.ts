export interface UserPreferences {
  id: string
  minDealValue: number
  quietHoursStart: string | null
  quietHoursEnd: string | null
  timezone: string
  enabledCategories: string[]
  interestTags: string[]
  createdAt: string
  updatedAt: string
}
