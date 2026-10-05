import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth-guard';
import { Login } from './features/auth/login/login';
import { Register } from './features/auth/register/register';
import { Workspace } from './features/workspace/workspace';

export const routes: Routes = [
  { path: '', component: Workspace, canActivate: [authGuard] },
  { path: 'login', component: Login, canActivate: [guestGuard] },
  { path: 'register', component: Register, canActivate: [guestGuard] },
  { path: '**', redirectTo: '' },
];
