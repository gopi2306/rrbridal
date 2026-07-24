import type { FormEvent } from 'react';
import { useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../../shared/auth';

export function LoginPage() {
  const { user, loading, login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [storeId, setStoreId] = useState('store-01');
  const [posCounter, setPosCounter] = useState('1');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  if (!loading && user) return <Navigate to="/" replace />;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await login({ email, password, storeId, posCounter, deviceId: `web-${storeId}-${posCounter}` });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={(e) => void onSubmit(e)}>
        <h1>
          Tru<span style={{ color: 'var(--blue)' }}>Billing</span>
        </h1>
        <p>Online store POS — sign in with your store account</p>
        {error && <div className="error">{error}</div>}
        <div className="field">
          <label>Email</label>
          <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required autoFocus />
        </div>
        <div className="field">
          <label>Password</label>
          <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required />
        </div>
        <div className="grid-2">
          <div className="field">
            <label>Store ID</label>
            <input value={storeId} onChange={(e) => setStoreId(e.target.value)} required />
          </div>
          <div className="field">
            <label>POS Counter</label>
            <input value={posCounter} onChange={(e) => setPosCounter(e.target.value)} required />
          </div>
        </div>
        <button className="btn btn-primary btn-block" disabled={busy} type="submit">
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
