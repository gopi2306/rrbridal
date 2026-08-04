/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_B2B_API_URL?: string
  readonly VITE_COMING_SOON?: string
}
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_COMING_SOON?: string
  readonly VITE_B2B_API_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
