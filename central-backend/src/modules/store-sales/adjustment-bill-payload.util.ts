type AdjustmentLinePayload = {
  lineNo?: unknown;
  adjustedQty?: unknown;
  adjustedRate?: unknown;
  adjustedAmount?: unknown;
  taxPercent?: unknown;
};

function readNumber(value: unknown, fallback = 0): number {
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : fallback;
}

function readInt(value: unknown, fallback = 0): number {
  return Math.trunc(readNumber(value, fallback));
}

function roundAmount(value: number): number {
  return Math.round(value * 10000) / 10000;
}

function computeLineTax(amount: number, taxPercent: number, isInterState: boolean) {
  if (isInterState) {
    return { cgst: 0, sgst: 0, igst: roundAmount((amount * taxPercent) / 100) };
  }
  const half = roundAmount((amount * (taxPercent / 2)) / 100);
  return { cgst: half, sgst: half, igst: 0 };
}

export function applyAdjustmentToBillPayload(
  currentPayload: Record<string, unknown>,
  adjustmentPayload: Record<string, unknown>,
): Record<string, unknown> {
  const isInterState =
    adjustmentPayload.isInterState === true || currentPayload.isInterState === true;
  const adjustmentLines = Array.isArray(adjustmentPayload.lines)
    ? (adjustmentPayload.lines as AdjustmentLinePayload[])
    : [];
  const byLineNo = new Map<number, AdjustmentLinePayload>();
  for (const line of adjustmentLines) {
    const lineNo = readInt(line.lineNo);
    if (lineNo > 0) byLineNo.set(lineNo, line);
  }

  const currentLines = Array.isArray(currentPayload.lines)
    ? (currentPayload.lines as Record<string, unknown>[])
    : [];
  const updatedLines = currentLines.map((line) => {
    const lineNo = readInt(line.lineNo);
    const adj = byLineNo.get(lineNo);
    if (!adj) return { ...line };

    const adjustedQty = readNumber(adj.adjustedQty);
    const adjustedRate = readNumber(adj.adjustedRate);
    const adjustedAmount = readNumber(adj.adjustedAmount);
    const taxPercent = readNumber(adj.taxPercent, readNumber(line.taxPercent));
    const tax = computeLineTax(adjustedAmount, taxPercent, isInterState);
    const taxAmount = tax.cgst + tax.sgst + tax.igst;

    return {
      ...line,
      qty: adjustedQty,
      rate: adjustedRate,
      amount: adjustedAmount,
      taxPercent,
      cgstPercent: isInterState ? 0 : taxPercent / 2,
      sgstPercent: isInterState ? 0 : taxPercent / 2,
      igstPercent: isInterState ? taxPercent : 0,
      cgstAmount: tax.cgst,
      sgstAmount: tax.sgst,
      igstAmount: tax.igst,
      taxAmount,
    };
  });

  let subTotal = 0;
  let cgstTotal = 0;
  let sgstTotal = 0;
  let igstTotal = 0;
  for (const line of updatedLines) {
    subTotal += readNumber(line.amount);
    cgstTotal += readNumber(line.cgstAmount);
    sgstTotal += readNumber(line.sgstAmount);
    igstTotal += readNumber(line.igstAmount);
  }

  const taxTotal = cgstTotal + sgstTotal + igstTotal;
  const adjustedPayable = readNumber(adjustmentPayload.adjustedPayable, subTotal + taxTotal);
  const roundOff = roundAmount(adjustedPayable - (subTotal + taxTotal));
  const adjustmentNo =
    typeof adjustmentPayload.adjustmentNo === 'string' ? adjustmentPayload.adjustmentNo.trim() : '';

  return {
    ...currentPayload,
    lines: updatedLines,
    subTotal,
    cgstTotal,
    sgstTotal,
    igstTotal,
    taxTotal,
    roundOff,
    payable: adjustedPayable,
    hasAdjustment: true,
    lastAdjustmentNo: adjustmentNo,
    adjustedAtUtc: new Date().toISOString(),
  };
}
