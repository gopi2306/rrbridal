export const AUTH_SETTINGS_KEY = 'default';

export const DEFAULT_ROLE_QUOTAS: Record<string, number> = {
  super_admin: 3,
  admin: 1,
  warehouse: 1,
  store: 5,
  procurement: 5,
};

export const SEED_ROLE_DEFINITIONS: Array<{
  code: string;
  displayName: string;
  description: string;
  sortOrder: number;
}> = [
  {
    code: 'super_admin',
    displayName: 'Super Admin',
    description: 'Company profile, limits, and role quotas; creates the operational admin once',
    sortOrder: 0,
  },
  {
    code: 'admin',
    displayName: 'Admin',
    description: 'Stores, locations, users, and day-to-day operations',
    sortOrder: 1,
  },
  { code: 'warehouse', displayName: 'Warehouse Manager', description: 'Warehouse operations', sortOrder: 2 },
  { code: 'store', displayName: 'Store Manager', description: 'Store / branch operations', sortOrder: 3 },
  { code: 'procurement', displayName: 'Procurement', description: 'Purchasing', sortOrder: 4 },
];
