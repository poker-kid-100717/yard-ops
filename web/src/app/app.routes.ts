import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'board' },
  { path: 'board', title: 'Yard Board', loadComponent: () => import('./pages/board').then(m => m.BoardPage) },
  { path: 'gate', title: 'Gate', loadComponent: () => import('./pages/gate').then(m => m.GatePage) },
  { path: 'trailers', title: 'Trailers', loadComponent: () => import('./pages/trailers').then(m => m.TrailersPage) },
  { path: 'trailers/:number', title: 'Trailer', loadComponent: () => import('./pages/trailer-detail').then(m => m.TrailerDetailPage) },
  { path: 'inspections', title: 'Inspections', loadComponent: () => import('./pages/inspections').then(m => m.InspectionsPage) },
  { path: 'outbox', title: 'LTL Outbox', loadComponent: () => import('./pages/outbox').then(m => m.OutboxPage) },
  { path: 'tms', title: 'TMS Trailers', loadComponent: () => import('./pages/tms').then(m => m.TmsPage) },
  { path: 'settings', title: 'Settings', loadComponent: () => import('./pages/settings').then(m => m.SettingsPage) },
  { path: '**', title: 'Not found', loadComponent: () => import('./pages/not-found').then(m => m.NotFoundPage) }
];
