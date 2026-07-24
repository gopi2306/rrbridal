export type JwtPayload = {
  sub: string;
  email: string;
  role: string;
  locationKind: string;
  storeId?: string;
  /** POS terminal device id (pos-v2 sessions). */
  deviceId?: string;
  /** Till / counter number as string (pos-v2 sessions). */
  posCounter?: string;
};
