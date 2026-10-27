import { Component } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CommonModule } from '@angular/common';
import { ToastComponent } from '../toast/toast.component';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { ToastService } from '../../services/toast.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  exact?: boolean;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive, ToastComponent],
  templateUrl: './layout.component.html',
  styleUrls: ['./layout.component.css']
})
export class LayoutComponent {
  user: any;
  now = new Date();
  passwordDialog = false;
  currentPassword = '';
  newPassword = '';
  changing = false;
  menuOpen = false;

  nav: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dash', exact: true },
    { path: '/pos', label: 'Point of Sale', icon: 'pos' },
    { path: '/orders', label: 'Orders & Receipts', icon: 'menu' },
    { path: '/kitchen', label: 'Kitchen (KDS)', icon: 'kitchen' },
    { path: '/tables', label: 'Tables & Floor', icon: 'tables' },
    { path: '/inventory', label: 'Inventory', icon: 'inventory' },
    { path: '/menu', label: 'Menu & Catalogue', icon: 'menu' },
    { path: '/reports', label: 'Reports', icon: 'reports' },
    { path: '/shifts', label: 'Shifts', icon: 'shifts' },
    { path: '/staff', label: 'Staff', icon: 'staff' },
    { path: '/audit', label: 'Audit Log', icon: 'reports' },
    { path: '/super-admin', label: 'Super Admin · Outlets', icon: 'tables' },
    { path: '/outlet-settings', label: 'My Outlet Settings', icon: 'tables' },
  ];

  constructor(private authService: AuthService, private router: Router, private api: ApiService, private toast: ToastService) {
    this.user = this.authService.getCurrentUser();
    const required: Record<string, string> = {'/dashboard':'ViewReports', '/pos':'CreateOrder', '/inventory':'ManageInventory', '/reports':'ViewReports', '/staff':'ManageUsers', '/audit':'ViewReports'};
    this.nav = this.nav.filter(item => item.path === '/super-admin' ? this.user?.roleName === 'Owner' : item.path === '/outlet-settings' ? this.user?.roleName === 'StoreManager' : this.user?.roleName === 'Owner' || !required[item.path] || this.user?.permissions?.includes(required[item.path]));
  }
  changePassword() {
    if (this.changing) return;
    this.changing = true;
    this.api.changePassword(this.currentPassword, this.newPassword).subscribe({next: () => {this.changing = false; this.authService.logout(); this.router.navigate(['/login']);}, error: e => {this.changing = false; this.toast.error(e?.error?.errors?.[0] || 'Password update failed.');}});
  }

  get initials(): string {
    const n = this.user?.username || 'U';
    return n.substring(0, 2).toUpperCase();
  }

  logout() {
    if (!confirm('Sign out of ORO POS?')) return;
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
