import { useState, type FormEvent } from 'react';
import { Navigate } from 'react-router-dom';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';

export function LoginPage() {
  const { ctx, login, loading } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [storeId, setStoreId] = useState('store-001');
  const [posCounter, setPosCounter] = useState('1');
  const [deviceId, setDeviceId] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  if (!loading && ctx) return <Navigate to="/" replace />;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      await login({ email, password, storeId, posCounter, deviceId: deviceId || undefined });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'Login failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={onSubmit}>
        <h1>TruBilling</h1>
        <p>Online store POS — same central APIs as WPF Online</p>
        <label>Email</label>
        <input className="field" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" required />
        <label>Password</label>
        <input className="field" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        <label>Store ID</label>
        <input className="field" value={storeId} onChange={(e) => setStoreId(e.target.value)} required />
        <label>POS counter</label>
        <input className="field" value={posCounter} onChange={(e) => setPosCounter(e.target.value)} required />
        <label>Device ID (optional)</label>
        <input className="field" value={deviceId} onChange={(e) => setDeviceId(e.target.value)} placeholder="web-store-001-1" />
        {error ? <div className="msg error" style={{ marginTop: 12 }}>{error}</div> : null}
        <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </div>
  );
}
