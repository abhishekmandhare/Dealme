// Maps raw OzBargain/Cheapies category tags → a canonical display group.
// A tag not matched here falls through to "Other".
const TAG_MAP: Record<string, string> = {
  // Computers
  computing: 'Computers', laptops: 'Computers', desktops: 'Computers',
  monitors: 'Computers', storage: 'Computers', networking: 'Computers',
  printers: 'Computers', components: 'Computers', peripherals: 'Computers',
  software: 'Computers',

  // Electronics
  electronics: 'Electronics', tv: 'Electronics', audio: 'Electronics',
  cameras: 'Electronics', 'home theatre': 'Electronics', headphones: 'Electronics',
  smartwatches: 'Electronics', wearables: 'Electronics',
  'electrical & electronics': 'Electronics',

  // Phones & Tablets
  mobile: 'Phones & Tablets', phones: 'Phones & Tablets', tablets: 'Phones & Tablets',
  'mobile phones': 'Phones & Tablets', smartphones: 'Phones & Tablets',

  // Gaming
  gaming: 'Gaming', consoles: 'Gaming', games: 'Gaming',
  'video games': 'Gaming', playstation: 'Gaming', xbox: 'Gaming', nintendo: 'Gaming',
  'video game': 'Gaming',

  // Travel
  travel: 'Travel', flights: 'Travel', hotels: 'Travel',
  accommodation: 'Travel', 'car hire': 'Travel', cruises: 'Travel',

  // Food & Drink
  food: 'Food & Drink', groceries: 'Food & Drink', dining: 'Food & Drink',
  restaurants: 'Food & Drink', 'food & drink': 'Food & Drink', alcohol: 'Food & Drink',
  coffee: 'Food & Drink', 'dining & takeaway': 'Food & Drink', takeaway: 'Food & Drink',
  snack: 'Food & Drink', 'snacks': 'Food & Drink',

  // Clothing & Fashion
  clothing: 'Clothing', fashion: 'Clothing', shoes: 'Clothing',
  accessories: 'Clothing', apparel: 'Clothing',

  // Home & Garden
  home: 'Home & Garden', garden: 'Home & Garden', furniture: 'Home & Garden',
  appliances: 'Home & Garden', kitchen: 'Home & Garden', tools: 'Home & Garden',
  'home & garden': 'Home & Garden', bedding: 'Home & Garden', lighting: 'Home & Garden',

  // Health & Beauty
  health: 'Health & Beauty', beauty: 'Health & Beauty', pharmacy: 'Health & Beauty',
  'personal care': 'Health & Beauty', vitamins: 'Health & Beauty', fitness: 'Health & Beauty',

  // Sports & Outdoors
  sports: 'Sports & Outdoors', outdoors: 'Sports & Outdoors', camping: 'Sports & Outdoors',
  cycling: 'Sports & Outdoors', gym: 'Sports & Outdoors',

  // Books & Media
  books: 'Books & Media', movies: 'Books & Media', music: 'Books & Media',
  entertainment: 'Books & Media', streaming: 'Books & Media', ebooks: 'Books & Media',

  // Automotive
  automotive: 'Automotive', cars: 'Automotive', 'car accessories': 'Automotive',
  tyres: 'Automotive', motoring: 'Automotive',

  // Toys & Kids
  toys: 'Toys & Kids', kids: 'Toys & Kids', baby: 'Toys & Kids', children: 'Toys & Kids',
  'toys & kids': 'Toys & Kids', lego: 'Toys & Kids', 'building blocks': 'Toys & Kids',

  // Internet & Telco
  internet: 'Internet & Telco', broadband: 'Internet & Telco', nbn: 'Internet & Telco',
  telco: 'Internet & Telco', 'mobile plan': 'Internet & Telco', sim: 'Internet & Telco',

  // Financial / Credit Cards
  cashback: 'Financial', 'credit card': 'Financial', 'credit cards': 'Financial',
  banking: 'Financial', insurance: 'Financial', finance: 'Financial',
  'earning points': 'Financial', 'points': 'Financial', rewards: 'Financial',

  // Pets
  pets: 'Pets', 'pet food': 'Pets', dog: 'Pets', cat: 'Pets',

  // Deals / Freebies (meta)
  freebies: 'Freebies', free: 'Freebies',
}

export function getCategory(tags: string[]): string {
  for (const tag of tags) {
    const group = TAG_MAP[tag.toLowerCase()]
    if (group) return group
  }
  return 'Other'
}

export function getCategories(opportunities: { tags: string[] }[]): string[] {
  const seen = new Set<string>()
  for (const o of opportunities) seen.add(getCategory(o.tags))
  return ['All', ...Array.from(seen).filter(c => c !== 'Other').sort(), ...(seen.has('Other') ? ['Other'] : [])]
}
