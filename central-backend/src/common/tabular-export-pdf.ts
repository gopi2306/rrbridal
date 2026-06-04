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

const HEADER_ROW_H = 22;
const BODY_ROW_H = 18;
const CELL_PAD_X = 5;
const CELL_PAD_Y = 4;
const FOOTER_H = 16;

function isRightAlignHeader(header: string): boolean {
  const h = header.toLowerCase();
  return (
    h.includes('qty') ||
    h.includes('transit') ||
    h.includes('mrp') ||
    h.includes('price') ||
    h.includes('cost') ||
    h.includes('selling')
  );
}

export function buildTabularPdfBuffer(
  title: string,
  contextLine: string,
  headers: readonly string[],
  rows: string[][],
): Promise<Buffer> {
  return new Promise((resolve, reject) => {
    const doc = new PDFDocument({ ...PAGE_OPTS, bufferPages: true });
    const chunks: Buffer[] = [];
    doc.on('data', (chunk: Buffer) => chunks.push(chunk));
    doc.on('end', () => resolve(Buffer.concat(chunks)));
    doc.on('error', reject);

    const left = doc.page.margins.left;
    const usableW = doc.page.width - doc.page.margins.left - doc.page.margins.right;
    const pageBottom = doc.page.height - doc.page.margins.bottom - FOOTER_H;
    const colW = Math.max(40, Math.floor(usableW / headers.length));
    const aligns = headers.map((h) => (isRightAlignHeader(h) ? 'right' : 'left') as 'left' | 'right');

    let y = doc.page.margins.top;
    doc.font('Helvetica-Bold').fontSize(17).fillColor(COLORS.title);
    doc.text(title, left, y, { width: usableW, lineBreak: false });
    y += 22;

    doc.font('Helvetica').fontSize(9.5).fillColor(COLORS.muted);
    doc.text(contextLine, left, y, { width: usableW, lineBreak: false });
    y += 16;

    doc.strokeColor(COLORS.rule).lineWidth(1).moveTo(left, y).lineTo(left + usableW, y).stroke();
    y += 14;

    const drawRow = (cells: string[], rowIndex: number, isHeader: boolean) => {
      const rowH = isHeader ? HEADER_ROW_H : BODY_ROW_H;

      if (isHeader) {
        doc.save();
        doc.rect(left, y, usableW, rowH).fill(COLORS.headerBg);
        doc.restore();
      } else if (rowIndex % 2 === 1) {
        doc.save();
        doc.rect(left, y, usableW, rowH).fill(COLORS.rowAlt);
        doc.restore();
      }

      let cx = left;
      doc.font(isHeader ? 'Helvetica-Bold' : 'Helvetica');
      doc.fontSize(isHeader ? 7.5 : 7);
      doc.fillColor(isHeader ? COLORS.headerText : COLORS.rowText);

      for (let i = 0; i < headers.length; i++) {
        const text = isHeader ? headers[i]! : (cells[i] ?? '');
        doc.text(text, cx + CELL_PAD_X, y + CELL_PAD_Y, {
          width: colW - CELL_PAD_X * 2,
          height: rowH - CELL_PAD_Y * 2,
          align: aligns[i],
          ellipsis: true,
          lineBreak: false,
        });
        cx += colW;
      }

      doc.strokeColor(COLORS.border).lineWidth(0.5);
      doc.moveTo(left, y + rowH).lineTo(left + usableW, y + rowH).stroke();
      y += rowH;
    };

    const drawTableHeader = () => {
      drawRow([], 0, true);
    };

    drawTableHeader();

    for (let i = 0; i < rows.length; i++) {
      if (y + BODY_ROW_H > pageBottom) {
        doc.addPage(PAGE_OPTS);
        y = doc.page.margins.top;
        drawTableHeader();
      }
      drawRow(rows[i] ?? [], i, false);
    }

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
