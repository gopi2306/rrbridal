import { formatLegacyReportDate } from './purchase-return-report.util';

export type LegacyReportDescriptionParams = {
  from: string;
  to: string;
  title: string;
  location?: string;
  entityLabel?: string;
  companyProfile?: { city?: string; gstin?: string; tradeName?: string } | null;
};

export function buildLegacyReportDescriptionRows(params: LegacyReportDescriptionParams): string[][] {
  const location =
    params.location?.trim() || params.companyProfile?.city?.trim() || '';
  const gstin = params.companyProfile?.gstin?.trim() || '';
  const gstPart = gstin ? `,GSTNO:${gstin}` : '';
  const entityLabel = (
    params.entityLabel?.trim() ||
    params.companyProfile?.tradeName?.trim() ||
    ''
  ).toUpperCase();
  const fromDate = formatLegacyReportDate(params.from);
  const toDate = formatLegacyReportDate(params.to);

  return [
    [`${location}${gstPart}`],
    [entityLabel],
    [params.title],
    [`DATE : from ${fromDate} ${toDate} ;`],
  ];
}
