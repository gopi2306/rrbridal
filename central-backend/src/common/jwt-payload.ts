export type JwtPayload = {
  sub: string;
  email: string;
  role: string;
  locationKind: string;
  storeId?: string;
};
