import type { ReactNode } from 'react';

export function Modal({
  title,
  children,
  footer,
  onClose,
  wide,
}: {
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
  wide?: boolean;
}) {
  return (
    <div className="modal-backdrop" onClick={onClose} role="presentation">
      <div
        className="modal"
        style={wide ? { width: 'min(920px, 100%)' } : undefined}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal
      >
        <div className="modal-h">
          <span>{title}</span>
          <button type="button" className="ghost-btn" style={{ width: 'auto' }} onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal-b">{children}</div>
        {footer ? <div className="modal-f">{footer}</div> : null}
      </div>
    </div>
  );
}
