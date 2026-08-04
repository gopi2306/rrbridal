import { useEffect, useState, type CSSProperties, type ImgHTMLAttributes } from "react";

interface SafeImageProps extends ImgHTMLAttributes<HTMLImageElement> {
  fallback: string;
  /** Per-image object-position for object-cover (e.g. "50% 18%"). Does not affect other instances. */
  objectPosition?: string;
}

export function SafeImage({
  src,
  fallback,
  alt,
  className,
  style,
  objectPosition,
  ...props
}: SafeImageProps) {
  const [current, setCurrent] = useState(src);

  useEffect(() => {
    setCurrent(src);
  }, [src]);

  const mergedStyle: CSSProperties = {
    ...style,
    ...(objectPosition != null ? { objectPosition } : {}),
  };

  const imgStyle =
    Object.keys(mergedStyle).length > 0 ? mergedStyle : undefined;

  const handleError = () => {
    if (current !== fallback) setCurrent(fallback);
  };

  return (
    <img
      {...props}
      src={current}
      alt={alt}
      className={className}
      style={imgStyle}
      onError={handleError}
    />
  );
}

interface OptimizedImageProps extends ImgHTMLAttributes<HTMLImageElement> {}

/** WebP image from /public. */
export function OptimizedImage({
  src = "",
  alt,
  className,
  ...props
}: OptimizedImageProps) {
  return <img src={src} alt={alt} className={className} {...props} />;
}
