import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../api/api-client.service';

export interface User { id: string; name: string; email: string; role?: 'customer' | 'admin' | 'seller'; avatar?: string; token?: string; phone?: string; }
export interface PendingCartAction { item: { productId: string; name: string; brand: string; image: string; price: number; variant: string; originalPrice?: number; discountPercent?: number; }; quantity: number; actionType: 'add' | 'buy'; returnUrl?: string; }
interface AuthResponse { token: string; expiresAt: string; user: { id: number; name: string; email: string; role: 'customer' | 'admin' | 'seller'; phone?: string; avatarUrl?: string; }; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly router = inject(Router); private readonly api = inject(ApiClient);
  private readonly USER_KEY = 'yourstore_auth_user'; private readonly TOKEN_KEY = 'yourstore_auth_token'; private readonly PENDING_KEY = 'yourstore_pending_cart_action';
  private readonly _currentUser = signal<User | null>(this.loadUser()); readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => !!this._currentUser() && !!this.token());
  private readonly _pendingAction = signal<PendingCartAction | null>(this.read<PendingCartAction>(this.PENDING_KEY)); readonly pendingAction = this._pendingAction.asReadonly();
  async login(email: string, password: string): Promise<{ success: boolean; message?: string; user?: User }> {
    if (!email.trim() || !password) return { success: false, message: 'Email and password are required.' };
    try { const response = await firstValueFrom(this.api.post<AuthResponse>('/auth/login', { email: email.trim(), password })); const user = this.acceptAuth(response); return { success: true, user }; }
    catch (error: any) { return { success: false, message: error?.status === 401 ? 'Incorrect email or password.' : (error?.error?.message || 'Unable to sign in. Is the API running?') }; }
  }
  async register(name: string, email: string, password: string): Promise<{ success: boolean; message?: string; user?: User }> {
    if (!name.trim() || !email.includes('@') || password.length < 8) return { success: false, message: 'Enter a name, valid email, and password of at least 8 characters.' };
    try { const response = await firstValueFrom(this.api.post<AuthResponse>('/auth/register', { name: name.trim(), email: email.trim(), password, phone: null })); const user = this.acceptAuth(response); return { success: true, user }; }
    catch (error: any) { return { success: false, message: error?.error?.message || 'Registration failed.' }; }
  }
  quickDemoLogin(): void { void this.login('priya@example.com', 'Customer123!'); }
  logout(): void { this._currentUser.set(null); this.remove(this.USER_KEY); this.remove(this.TOKEN_KEY); }
  token(): string | null { return typeof window !== 'undefined' ? localStorage.getItem(this.TOKEN_KEY) : null; }
  setPendingCartAction(action: PendingCartAction): void { this._pendingAction.set(action); this.write(this.PENDING_KEY, action); }
  consumePendingCartAction(): PendingCartAction | null { const value = this._pendingAction(); this.clearPendingCartAction(); return value; }
  clearPendingCartAction(): void { this._pendingAction.set(null); this.remove(this.PENDING_KEY); }
  requireAuthForCart(action: PendingCartAction): boolean { if (this.isAuthenticated()) return true; this.setPendingCartAction(action); void this.router.navigate(['/login'], { queryParams: { returnUrl: action.returnUrl || this.router.url, reason: action.actionType === 'buy' ? 'buynow' : 'cart', productName: action.item.name } }); return false; }
  private acceptAuth(response: AuthResponse): User { const user: User = { id: String(response.user.id), name: response.user.name, email: response.user.email, role: response.user.role, phone: response.user.phone, avatar: response.user.avatarUrl, token: response.token }; this._currentUser.set(user); this.write(this.USER_KEY, user); if (typeof window !== 'undefined') localStorage.setItem(this.TOKEN_KEY, response.token); return user; }
  private loadUser(): User | null { return this.token() ? this.read<User>(this.USER_KEY) : null; }
  private read<T>(key: string): T | null { try { return typeof window === 'undefined' ? null : JSON.parse(localStorage.getItem(key) || 'null'); } catch { return null; } }
  private write(key: string, value: unknown): void { if (typeof window !== 'undefined') localStorage.setItem(key, JSON.stringify(value)); }
  private remove(key: string): void { if (typeof window !== 'undefined') localStorage.removeItem(key); }
}
