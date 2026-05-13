export const AUTH_SETTINGS_KEY = 'default';

export const DEFAULT_ROLE_QUOTAS: Record<string, number> = {
  admin: 5,
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
  { code: 'admin', displayName: 'Admin', description: 'Full access', sortOrder: 0 },
  { code: 'warehouse', displayName: 'Warehouse', description: 'Warehouse operations', sortOrder: 1 },
  { code: 'store', displayName: 'Store', description: 'Store / branch operations', sortOrder: 2 },
  { code: 'procurement', displayName: 'Procurement', description: 'Purchasing', sortOrder: 3 },
];
