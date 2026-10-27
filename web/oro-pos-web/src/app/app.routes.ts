import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { LayoutComponent } from './components/layout/layout.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PosComponent } from './components/pos/pos.component';
import { KitchenComponent } from './components/kitchen/kitchen.component';
import { TablesComponent } from './components/tables/tables.component';
import { ReportsComponent } from './components/reports/reports.component';
import { ShiftsComponent } from './components/shifts/shifts.component';
import { InventoryComponent } from './components/inventory/inventory.component';
import { MenuComponent } from './components/menu/menu.component';
import { StaffComponent } from './components/staff/staff.component';
import { AuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [AuthGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardComponent },
      { path: 'pos', component: PosComponent },
      { path: 'kitchen', component: KitchenComponent },
      { path: 'tables', component: TablesComponent },
      { path: 'inventory', component: InventoryComponent },
      { path: 'menu', component: MenuComponent },
      { path: 'reports', component: ReportsComponent },
      { path: 'shifts', component: ShiftsComponent },
      { path: 'staff', component: StaffComponent },
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
