import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PosComponent } from './components/pos/pos.component';
import { KitchenComponent } from './components/kitchen/kitchen.component';
import { TablesComponent } from './components/tables/tables.component';
import { ReportsComponent } from './components/reports/reports.component';
import { InventoryComponent } from './components/inventory/inventory.component';
import { MenuComponent } from './components/menu/menu.component';
import { StaffComponent } from './components/staff/staff.component';
import { AuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent, canActivate: [AuthGuard] },
  { path: 'pos', component: PosComponent, canActivate: [AuthGuard] },
  { path: 'kitchen', component: KitchenComponent, canActivate: [AuthGuard] },
  { path: 'tables', component: TablesComponent, canActivate: [AuthGuard] },
  { path: 'inventory', component: InventoryComponent, canActivate: [AuthGuard] },
  { path: 'menu', component: MenuComponent, canActivate: [AuthGuard] },
  { path: 'reports', component: ReportsComponent, canActivate: [AuthGuard] },
  { path: 'staff', component: StaffComponent, canActivate: [AuthGuard] },
  { path: '**', redirectTo: '/dashboard' }
];
