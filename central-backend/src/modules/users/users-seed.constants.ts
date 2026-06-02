/** Canonical bootstrap super admin (created on first run if missing). */
export const CANONICAL_SUPER_ADMIN_EMAIL = 'admin@gmail.com';

export const SEED_DEV_OPS_EMAIL = 'ops-admin@rrbridal.com';

export const SEED_STORE_USER_EMAILS = [
  'ravi@rrbridal.com',
  'priya@rrbridal.com',
  'anand@rrbridal.com',
  'deepa@rrbridal.com',
  'vijay@rrbridal.com',
  'kavitha@rrbridal.com',
  'suresh@rrbridal.com',
] as const;

export const NON_SUPER_ADMIN_ROLE_CODES = ['admin', 'warehouse', 'store', 'procurement'] as const;

export function isTruthyEnv(value: string | undefined): boolean {
  return ['1', 'true', 'yes'].includes(String(value ?? '').toLowerCase());
}

/** Production: only super_admin role + user; no demo stores/users. */
export function isProductionSeed(): boolean {
  return isTruthyEnv(process.env.SEED_PRODUCTION);
}
