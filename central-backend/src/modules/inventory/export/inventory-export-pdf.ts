import PDFDocument from 'pdfkit';

const MARGIN = 24;
const PAGE_OPTS = { layout: 'landscape' as const, size: 'A4' as const, margin: MARGIN };

const COLORS = {
  title: '#0f172a',
  muted: '#64748b',
  headerBg: '#1e3a8a',
  headerText: '#ffffff',
  border: '#cbd5e1',
  rowAlt: '#f8fafc',
  rowText: '#1e293b',
  rule: '#e2e8f0',
};

type ColAlign = 'left' | 'right';

type PdfColumn = {
  header: string;
  weight: number;
  align: ColAlign;
  width?: number;
};

/** Relative column weights — scaled to full usable page width at render time. */
const PDF_COLUMN_DEFS: PdfColumn[] = [
  { header: 'SKU', weight: 9, align: 'left' },
  { header: 'Barcode', weight: 8, align: 'left' },
  { header: 'Product', weight: 15, align: 'left' },
  { header: 'Brand', weight: 8, align: 'left' },
  { header: 'Category', weight: 8, align: 'left' },
  { header: 'Wh qty', weight: 5, align: 'right' },
  { header: 'In transit', weight: 6, align: 'right' },
  { header: 'Store qty', weight: 6, align: 'right' },
  { header: 'Cost', weight: 7, align: 'right' },
  { header: 'MRP', weight: 7, align: 'right' },
  { header: 'Selling', weight: 7, align: 'right' },
  { header: 'Store ₹', weight: 7, align: 'right' },
  { header: 'GST %', weight: 4, align: 'right' },
];

const HEADER_ROW_H = 22;
const BODY_ROW_H = 18;
const CELL_PAD_X = 5;
const CELL_PAD_Y = 4;
const FOOTER_H = 16;

function scaleColumns(usableWidth: number): PdfColumn[] {
  const totalWeight = PDF_COLUMN_DEFS.reduce((s, c) => s + c.weight, 0);
  const cols = PDF_COLUMN_DEFS.map((c) => ({
    ...c,
    width: Math.floor((usableWidth * c.weight) / totalWeight),
  }));
  const assigned = cols.reduce((s, c) => s + (c.width ?? 0), 0);
  const last = cols[cols.length - 1];
  if (last) last.width = (last.width ?? 0) + (usableWidth - assigned);
  return cols;
}

function formatContextParts(contextLine: string): { store: string; search?: string; generated: string } {
  const parts = contextLine.split(' · ').map((p) => p.trim());
  let store = 'All stores';
  let search: string | undefined;
  let generated = new Date().toISOString();

  for (const part of parts) {
    if (part.startsWith('Store:')) store = part.slice('Store:'.length).trim();
    else if (part.startsWith('Search:')) search = part.slice('Search:'.length).trim();
    else if (part.startsWith('Generated:')) generated = part.slice('Generated:'.length).trim();
  }

  const result: { store: string; search?: string; generated: string } = { store, generated };
  if (search) result.search = search;
  return result;
}

function formatGeneratedLabel(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('en-IN', {
    timeZone: 'UTC',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

function drawTitleBlock(
  doc: PDFKit.PDFDocument,
  left: number,
  width: number,
  startY: number,
  meta: { store: string; search?: string; generated: string },
  rowCount: number,
): number {
  let y = startY;

  doc.font('Helvetica-Bold').fontSize(17).fillColor(COLORS.title);
  doc.text('Inventory Report', left, y, { width, lineBreak: false });
  y += 22;

  doc.font('Helvetica').fontSize(9.5).fillColor(COLORS.muted);
  doc.text(`Store: ${meta.store}`, left, y, { width, lineBreak: false });
  y += 13;

  if (meta.search) {
    doc.text(`Search: ${meta.search}`, left, y, { width, lineBreak: false });
    y += 13;
  }

  doc.text(
    `Generated: ${formatGeneratedLabel(meta.generated)} UTC · ${rowCount} product(s)`,
    left,
    y,
    { width, lineBreak: false },
  );
  y += 16;

  doc.strokeColor(COLORS.rule).lineWidth(1).moveTo(left, y).lineTo(left + width, y).stroke();
  y += 14;

  return y;
}

export function buildInventoryPdfBuffer(rows: string[][], contextLine: string): Promise<Buffer> {
  return new Promise((resolve, reject) => {
    const doc = new PDFDocument({ ...PAGE_OPTS, bufferPages: true });
    const chunks: Buffer[] = [];
    doc.on('data', (chunk: Buffer) => chunks.push(chunk));
    doc.on('end', () => resolve(Buffer.concat(chunks)));
    doc.on('error', reject);

    const left = doc.page.margins.left;
    const usableW = doc.page.width - doc.page.margins.left - doc.page.margins.right;
    const pageBottom = doc.page.height - doc.page.margins.bottom - FOOTER_H;
    const cols = scaleColumns(usableW);
    const meta = formatContextParts(contextLine);

    let tableTopY = 0;
    let y = drawTitleBlock(doc, left, usableW, doc.page.margins.top, meta, rows.length);

    const drawVerticalBorders = (top: number, bottom: number) => {
      doc.save();
      doc.strokeColor(COLORS.border).lineWidth(0.5);
      let x = left;
      for (const col of cols) {
        doc.moveTo(x, top).lineTo(x, bottom).stroke();
        x += col.width ?? 0;
      }
      doc.moveTo(x, top).lineTo(x, bottom).stroke();
      doc.restore();
    };

    const drawRow = (cells: string[], rowIndex: number, isHeader: boolean) => {
      const rowH = isHeader ? HEADER_ROW_H : BODY_ROW_H;

      if (isHeader) {
        doc.save();
        doc.rect(left, y, usableW, rowH).fill(COLORS.headerBg);
        doc.restore();
        doc.strokeColor(COLORS.border).lineWidth(0.5);
        doc.moveTo(left, y).lineTo(left + usableW, y).stroke();
      } else if (rowIndex % 2 === 1) {
        doc.save();
        doc.rect(left, y, usableW, rowH).fill(COLORS.rowAlt);
        doc.restore();
      }

      let cx = left;
      doc.font(isHeader ? 'Helvetica-Bold' : 'Helvetica');
      doc.fontSize(isHeader ? 7.5 : 7);
      doc.fillColor(isHeader ? COLORS.headerText : COLORS.rowText);

      for (let i = 0; i < cols.length; i++) {
        const col = cols[i]!;
        const colW = col.width ?? 0;
        const text = isHeader ? col.header : (cells[i] ?? '');
        doc.text(text, cx + CELL_PAD_X, y + CELL_PAD_Y, {
          width: colW - CELL_PAD_X * 2,
          height: rowH - CELL_PAD_Y * 2,
          align: col.align,
          ellipsis: true,
          lineBreak: false,
        });
        cx += colW;
      }

      doc.strokeColor(COLORS.border).lineWidth(0.5);
      doc.moveTo(left, y + rowH).lineTo(left + usableW, y + rowH).stroke();

      y += rowH;
    };

    const startNewPage = () => {
      doc.addPage(PAGE_OPTS);
      y = doc.page.margins.top;
      tableTopY = y;
      drawRow([], 0, true);
      drawVerticalBorders(tableTopY, y);
    };

    tableTopY = y;
    drawRow([], 0, true);

    for (let i = 0; i < rows.length; i++) {
      if (y + BODY_ROW_H > pageBottom) {
        drawVerticalBorders(tableTopY, y);
        startNewPage();
      }
      drawRow(rows[i] ?? [], i, false);
    }

    drawVerticalBorders(tableTopY, y);

    const range = doc.bufferedPageRange();
    const pageCount = range.count;
    for (let p = range.start; p < range.start + pageCount; p++) {
      doc.switchToPage(p);
      const footerY = doc.page.height - doc.page.margins.bottom - 10;
      doc.font('Helvetica').fontSize(8).fillColor(COLORS.muted);
      doc.text(`Page ${p - range.start + 1} of ${pageCount}`, left, footerY, {
        width: usableW,
        align: 'right',
        lineBreak: false,
      });
    }

    doc.flushPages();
    doc.end();
  });
}
